using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Memory;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Orchestration;
using CrestApps.Core.AI.Services;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Infrastructure.Indexing;
using CrestApps.Core.Infrastructure.Indexing.Models;
using CrestApps.Core.Templates.Services;
using Cysharp.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.Core.AI.Chat.Handlers;

/// <summary>
/// Preemptively retrieves relevant document chunks for uploaded documents or profile knowledge documents
/// and injects them into the orchestration system message before model generation begins.
/// </summary>
internal sealed class DocumentPreemptiveRagHandler : IPreemptiveRagHandler
{
    private readonly IAIClientFactory _aiClientFactory;
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly ISearchIndexProfileStore _indexProfileStore;
    private readonly ITemplateService _templateService;
    private readonly InteractionDocumentOptions _options;
    private readonly IAITextNormalizer _textNormalizer;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    public DocumentPreemptiveRagHandler(
        IAIClientFactory aiClientFactory,
        IAIDeploymentManager deploymentManager,
        ISearchIndexProfileStore indexProfileStore,
        ITemplateService templateService,
        IOptions<InteractionDocumentOptions> options,
        IAITextNormalizer textNormalizer,
        IServiceProvider serviceProvider,
        ILogger<DocumentPreemptiveRagHandler> logger)
    {
        _aiClientFactory = aiClientFactory;
        _deploymentManager = deploymentManager;
        _indexProfileStore = indexProfileStore;
        _templateService = templateService;
        _options = options.Value;
        _textNormalizer = textNormalizer;
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
        if (string.IsNullOrEmpty(_options.IndexProfileName))
        {
            return;
        }

        try
        {
            await InjectPreemptiveRagContextAsync(context, _options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during document Preemptive RAG injection.");
        }
    }

    private async Task InjectPreemptiveRagContextAsync(
        PreemptiveRagContext context,
        InteractionDocumentOptions settings)
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

        var metadata = SearchIndexProfileEmbeddingMetadataAccessor.GetMetadata(indexProfile);
        var embeddingGenerator = await EmbeddingDeploymentResolver.CreateEmbeddingGeneratorAsync(
            _deploymentManager,
            _aiClientFactory,
            metadata,
            indexProfile.EmbeddingDeploymentId);

        if (embeddingGenerator == null)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Document Preemptive RAG: embedding deployment is not configured or could not be resolved on index profile '{IndexProfileName}'. DeploymentId={DeploymentId}.",
                    settings.IndexProfileName,
                    metadata?.EmbeddingDeploymentId ?? indexProfile.EmbeddingDeploymentId ?? "(null)");
            }

            return;
        }

        var embeddings = await embeddingGenerator.GenerateAsync(context.Queries);

        if (embeddings == null || embeddings.Count == 0)
        {
            return;
        }

        var searchScopes = new List<(string ResourceId, string ReferenceType)>();

        if (context.Resource is ChatInteraction interaction)
        {
            searchScopes.Add((interaction.ItemId, AIReferenceTypes.Document.ChatInteraction));
        }
        else if (context.Resource is AIProfile profile)
        {
            searchScopes.Add((profile.ItemId, AIReferenceTypes.Document.Profile));

            if (context.OrchestrationContext.CompletionContext?.AdditionalProperties is not null &&
                context.OrchestrationContext.CompletionContext.AdditionalProperties.TryGetValue("Session", out var sessionObject) &&
                sessionObject is AIChatSession session &&
                session.Documents is { Count: > 0 })
            {
                searchScopes.Add((session.SessionId, AIReferenceTypes.Document.ChatSession));
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
            searchScopes.Any(scope => scope.ReferenceType == AIReferenceTypes.Document.ChatSession);

        var topN = settings.TopN > 0 ? settings.TopN : 3;

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Document Preemptive RAG: searching {ScopeCount} scope(s): [{Scopes}] with {QueryCount} queries, topN={TopN}.",
                searchScopes.Count,
                string.Join(", ", searchScopes.Select(s => $"{s.ReferenceType}:{s.ResourceId}")),
                context.Queries.Count,
                topN);
        }

        var allResults = new List<(DocumentChunkSearchResult Result, string ReferenceType)>();
        var seenChunkKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hasProfileScope = searchScopes.Any(scope => scope.ReferenceType == AIReferenceTypes.Document.Profile);
        var hasSessionScope = searchScopes.Any(scope => scope.ReferenceType == AIReferenceTypes.Document.ChatSession);
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
                    indexProfile,
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

                    var chunkKey = $"{result.DocumentKey}:{result.Chunk.Index}";

                    if (seenChunkKeys.Add(chunkKey))
                    {
                        allResults.Add((result, scopeReferenceType));
                    }
                }
            }
        }

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

        using var builder = ZString.CreateStringBuilder();

        var arguments = new Dictionary<string, object>();
        var hasUserSuppliedDocumentContext = finalResults.Any(x =>
            x.ReferenceType == AIReferenceTypes.Document.ChatInteraction ||
            x.ReferenceType == AIReferenceTypes.Document.ChatSession);
        var hasKnowledgeBaseDocumentContext = finalResults.Any(x => x.ReferenceType == AIReferenceTypes.Document.Profile);

        if (!orchestrationContext.DisableTools)
        {
            arguments["searchToolName"] = SystemToolNames.SearchDocuments;
        }

        arguments["hasUserSuppliedDocumentContext"] = hasUserSuppliedDocumentContext;
        arguments["hasKnowledgeBaseDocumentContext"] = hasKnowledgeBaseDocumentContext;

        var header = await _templateService.RenderAsync(AITemplateIds.DocumentContextHeader, arguments);

        if (!string.IsNullOrEmpty(header))
        {
            builder.AppendLine();
            builder.AppendLine();
            builder.Append(header);
        }

        if (showUserDocumentAwareness)
        {
            var invocationContext = AIInvocationScope.Current;
            var seenDocuments = new Dictionary<string, (int Index, string FileName)>(StringComparer.OrdinalIgnoreCase);

            foreach (var (result, scopeReferenceType) in finalResults)
            {
                if (!keepProfileDocumentAwareness && scopeReferenceType == AIReferenceTypes.Document.Profile)
                {
                    builder.AppendLine("---");
                    builder.AppendLine(result.Chunk.Text);
                    continue;
                }

                var documentKey = result.DocumentKey;

                if (!string.IsNullOrEmpty(documentKey) && !seenDocuments.ContainsKey(documentKey))
                {
                    seenDocuments[documentKey] = (invocationContext?.NextReferenceIndex() ?? seenDocuments.Count + 1, _textNormalizer.NormalizeTitle(result.FileName));
                }

                var referenceIndex = !string.IsNullOrEmpty(documentKey) && seenDocuments.TryGetValue(documentKey, out var entry)
                    ? entry.Index
                    : invocationContext?.NextReferenceIndex() ?? seenDocuments.Count + 1;

                builder.AppendLine("---");
                builder.Append("[doc:");
                builder.Append(referenceIndex);
                builder.Append("] ");
                builder.AppendLine(result.Chunk.Text);
            }

            if (seenDocuments.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("References:");

                foreach (var kvp in seenDocuments)
                {
                    builder.Append("[doc:");
                    builder.Append(kvp.Value.Index);
                    builder.Append("] = {DocumentId: \"");
                    builder.Append(kvp.Key);
                    builder.Append('"');

                    if (!string.IsNullOrWhiteSpace(kvp.Value.FileName))
                    {
                        builder.Append(", FileName: \"");
                        builder.Append(kvp.Value.FileName);
                        builder.Append('"');
                    }

                    builder.AppendLine("}");
                }

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
                        ReferenceType = AIReferenceTypes.DataSource.Document,
                    };
                }

                orchestrationContext.Properties["DocumentReferences"] = citationMap;
            }
        }
        else
        {
            foreach (var (result, _) in finalResults)
            {
                builder.AppendLine("---");
                builder.AppendLine(result.Chunk.Text);
            }
        }

        orchestrationContext.SystemMessageBuilder.Append(builder);
    }
}
