using CrestApps.AI;
using CrestApps.AI.Chat.Models;
using CrestApps.AI.Models;
using CrestApps.AI.Prompting.Services;
using CrestApps.AI.Services;
using CrestApps.OrchardCore.AI.Core;
using Cysharp.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Entities;
using OrchardCore.Indexing;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Documents.Handlers;

/// <summary>
/// Preemptive RAG handler for uploaded documents. Receives pre-extracted search queries
/// from the coordinator, embeds them, searches the document vector index, and appends
/// relevant chunks to the system message.
/// </summary>
internal sealed class DocumentPreemptiveRagHandler : IPreemptiveRagHandler
{
    private readonly IAIClientFactory _aiClientFactory;
    private readonly IIndexProfileStore _indexProfileStore;
    private readonly IAITemplateService _templateService;
    private readonly ISiteService _siteService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    public DocumentPreemptiveRagHandler(
        IAIClientFactory aiClientFactory,
        IIndexProfileStore indexProfileStore,
        IAITemplateService templateService,
        ISiteService siteService,
        IServiceProvider serviceProvider,
        ILogger<DocumentPreemptiveRagHandler> logger)
    {
        _aiClientFactory = aiClientFactory;
        _indexProfileStore = indexProfileStore;
        _templateService = templateService;
        _siteService = siteService;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public ValueTask<bool> CanHandleAsync(OrchestrationContextBuiltContext context)
    {
        if (context.OrchestrationContext.Documents is { Count: > 0 })
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("DocumentPreemptiveRagHandler can handle: {DocCount} document(s) found in orchestration context.", context.OrchestrationContext.Documents.Count);
            }

            return ValueTask.FromResult(true);
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("DocumentPreemptiveRagHandler skipped: no documents in orchestration context.");
        }

        return ValueTask.FromResult(false);
    }

    public async Task HandleAsync(PreemptiveRagContext context)
    {
        var settings = await _siteService.GetSettingsAsync<InteractionDocumentSettings>();

        if (string.IsNullOrEmpty(settings.IndexProfileName))
        {
            return;
        }

        try
        {
            await InjectPreemptiveRagContextAsync(context, settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during document Preemptive RAG injection.");
        }
    }

    private async Task InjectPreemptiveRagContextAsync(
        PreemptiveRagContext context,
        InteractionDocumentSettings settings)
    {
        var indexProfile = await _indexProfileStore.FindByNameAsync(settings.IndexProfileName);

        if (indexProfile == null)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Document Preemptive RAG: index profile '{IndexProfileName}' not found.", settings.IndexProfileName);
            }

            return;
        }

        var searchService = _serviceProvider.GetKeyedService<IVectorSearchService>(indexProfile.ProviderName);

        if (searchService == null)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Document Preemptive RAG: no IVectorSearchService registered for provider '{ProviderName}'.", indexProfile.ProviderName);
            }

            return;
        }

        // Get embedding configuration from the index profile metadata.
        var interactionMetadata = indexProfile.As<ChatInteractionIndexProfileMetadata>();

        if (string.IsNullOrEmpty(interactionMetadata?.EmbeddingProviderName) ||
            string.IsNullOrEmpty(interactionMetadata.EmbeddingConnectionName) ||
            string.IsNullOrEmpty(interactionMetadata.EmbeddingDeploymentName))
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Document Preemptive RAG: embedding configuration incomplete on index profile '{IndexProfileName}'. Provider={Provider}, Connection={Connection}, Deployment={Deployment}.",
                    settings.IndexProfileName,
                    interactionMetadata?.EmbeddingProviderName ?? "(null)",
                    interactionMetadata?.EmbeddingConnectionName ?? "(null)",
                    interactionMetadata?.EmbeddingDeploymentName ?? "(null)");
            }

            return;
        }

        var embeddingGenerator = await _aiClientFactory.CreateEmbeddingGeneratorAsync(
            interactionMetadata.EmbeddingProviderName,
            interactionMetadata.EmbeddingConnectionName,
            interactionMetadata.EmbeddingDeploymentName);

        if (embeddingGenerator == null)
        {
            return;
        }

        // Generate embeddings for all search queries.
        var embeddings = await embeddingGenerator.GenerateAsync(context.Queries);

        if (embeddings == null || embeddings.Count == 0)
        {
            return;
        }

        // Resolve the resource scopes to search across.
        var searchScopes = new List<(string ResourceId, string ReferenceType)>();

        if (context.Resource is ChatInteraction interaction)
        {
            searchScopes.Add((interaction.ItemId, AIConstants.DocumentReferenceTypes.ChatInteraction));
        }
        else if (context.Resource is AIProfile profile)
        {
            searchScopes.Add((profile.ItemId, AIConstants.DocumentReferenceTypes.Profile));

            // Also search session documents if a session with documents exists.
            if (context.OrchestrationContext.CompletionContext?.AdditionalProperties is not null &&
                context.OrchestrationContext.CompletionContext.AdditionalProperties.TryGetValue("Session", out var sessionObj) &&
                sessionObj is AIChatSession session &&
                session.Documents is { Count: > 0 })
            {
                searchScopes.Add((session.SessionId, AIConstants.DocumentReferenceTypes.ChatSession));
            }
        }

        if (searchScopes.Count == 0)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Document Preemptive RAG: no search scopes resolved for resource type '{ResourceType}'.", context.Resource?.GetType().Name);
            }

            return;
        }

        var showUserDocumentAwareness =
            context.Resource is not AIProfile ||
            searchScopes.Any(scope => scope.ReferenceType == AIConstants.DocumentReferenceTypes.ChatSession);

        var topN = settings.TopN > 0 ? settings.TopN : 3;

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Document Preemptive RAG: searching {ScopeCount} scope(s): [{Scopes}] with {QueryCount} queries, topN={TopN}.",
                searchScopes.Count,
                string.Join(", ", searchScopes.Select(s => $"{s.ReferenceType}:{s.ResourceId}")),
                context.Queries.Count,
                topN);
        }

        // Perform vector search for each embedding across all scopes and combine results.
        var allResults = new List<(DocumentChunkSearchResult Result, string ReferenceType)>();
        var seenChunkKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hasProfileScope = searchScopes.Any(scope => scope.ReferenceType == AIConstants.DocumentReferenceTypes.Profile);
        var hasSessionScope = searchScopes.Any(scope => scope.ReferenceType == AIConstants.DocumentReferenceTypes.ChatSession);
        var keepProfileDocumentAwareness = !(context.Resource is AIProfile && hasProfileScope && hasSessionScope);

        foreach (var embedding in embeddings)
        {
            if (embedding?.Vector == null)
            {
                continue;
            }

            foreach (var (scopeResourceId, scopeReferenceType) in searchScopes)
            {
                var results = await searchService.SearchAsync(
                    indexProfile.ToIndexProfileInfo(),
                    embedding.Vector.ToArray(),
                    scopeResourceId,
                    scopeReferenceType,
                    topN);

                if (results == null)
                {
                    continue;
                }

                foreach (var result in results)
                {
                    if (result.Chunk == null || string.IsNullOrWhiteSpace(result.Chunk.Text))
                    {
                        continue;
                    }

                    // Deduplicate by chunk key to avoid injecting the same chunk multiple times.
                    var chunkKey = $"{result.DocumentKey}:{result.Chunk.Index}";

                    if (seenChunkKeys.Add(chunkKey))
                    {
                        allResults.Add((result, scopeReferenceType));
                    }
                }
            }
        }

        // Sort by score descending and take top N.
        var finalResults = allResults
            .OrderByDescending(r => r.Result.Score)
            .Take(topN)
            .ToList();

        if (finalResults.Count == 0)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Document Preemptive RAG: no relevant chunks found after vector search.");
            }

            return;
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Document Preemptive RAG: injecting {ResultCount} chunk(s) into system message.", finalResults.Count);
        }

        var orchestrationContext = context.OrchestrationContext;

        // Build context injection.
        using var sb = ZString.CreateStringBuilder();

        var arguments = new Dictionary<string, object>();
        var hasUserSuppliedDocumentContext = finalResults.Any(x =>
            x.ReferenceType == AIConstants.DocumentReferenceTypes.ChatInteraction ||
            x.ReferenceType == AIConstants.DocumentReferenceTypes.ChatSession);
        var hasKnowledgeBaseDocumentContext = finalResults.Any(x => x.ReferenceType == AIConstants.DocumentReferenceTypes.Profile);

        if (!orchestrationContext.DisableTools)
        {
            arguments["searchToolName"] = SystemToolNames.SearchDocuments;
        }
        arguments["hasUserSuppliedDocumentContext"] = hasUserSuppliedDocumentContext;
        arguments["hasKnowledgeBaseDocumentContext"] = hasKnowledgeBaseDocumentContext;

        var header = await _templateService.RenderAsync(AITemplateIds.DocumentContextHeader, arguments);

        if (!string.IsNullOrEmpty(header))
        {
            sb.AppendLine();
            sb.AppendLine();
            sb.Append(header);
        }

        if (showUserDocumentAwareness)
        {
            var invocationContext = AIInvocationScope.Current;
            var seenDocuments = new Dictionary<string, (int Index, string FileName)>(StringComparer.OrdinalIgnoreCase);

            foreach (var (result, scopeReferenceType) in finalResults)
            {
                if (!keepProfileDocumentAwareness && scopeReferenceType == AIConstants.DocumentReferenceTypes.Profile)
                {
                    sb.AppendLine("---");
                    sb.AppendLine(result.Chunk.Text);
                    continue;
                }

                var documentKey = result.DocumentKey;

                if (!string.IsNullOrEmpty(documentKey) && !seenDocuments.ContainsKey(documentKey))
                {
                    seenDocuments[documentKey] = (invocationContext?.NextReferenceIndex() ?? seenDocuments.Count + 1, RagTextNormalizer.NormalizeTitle(result.FileName));
                }

                var refIdx = !string.IsNullOrEmpty(documentKey) && seenDocuments.TryGetValue(documentKey, out var entry)
                    ? entry.Index
                    : invocationContext?.NextReferenceIndex() ?? seenDocuments.Count + 1;

                sb.AppendLine("---");
                sb.Append("[doc:");
                sb.Append(refIdx);
                sb.Append("] ");
                sb.AppendLine(result.Chunk.Text);
            }

            if (seenDocuments.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("References:");

                foreach (var kvp in seenDocuments)
                {
                    sb.Append("[doc:");
                    sb.Append(kvp.Value.Index);
                    sb.Append("] = {DocumentId: \"");
                    sb.Append(kvp.Key);
                    sb.Append('"');

                    if (!string.IsNullOrWhiteSpace(kvp.Value.FileName))
                    {
                        sb.Append(", FileName: \"");
                        sb.Append(kvp.Value.FileName);
                        sb.Append('"');
                    }

                    sb.AppendLine("}");
                }

                // Store citation metadata on the orchestration context for downstream consumers.
                var citationMap = new Dictionary<string, AICompletionReference>();

                foreach (var kvp in seenDocuments)
                {
                    var template = $"[doc:{kvp.Value.Index}]";
                    citationMap[template] = new AICompletionReference
                    {
                        Text = string.IsNullOrWhiteSpace(kvp.Value.FileName) ? template : kvp.Value.FileName,
                        Title = kvp.Value.FileName,
                        Index = kvp.Value.Index,
                        ReferenceId = kvp.Key,
                        ReferenceType = AIConstants.DataSourceReferenceTypes.Document,
                    };
                }

                orchestrationContext.Properties["DocumentReferences"] = citationMap;
            }
        }
        else
        {
            foreach (var (result, _) in finalResults)
            {
                sb.AppendLine("---");
                sb.AppendLine(result.Chunk.Text);
            }
        }

        orchestrationContext.SystemMessageBuilder.Append(sb);
    }
}
