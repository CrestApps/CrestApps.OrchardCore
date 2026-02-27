using CrestApps.AI.Prompting.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Cysharp.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Entities;
using OrchardCore.Indexing;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.DataSources.Handlers;

/// <summary>
/// Preemptive RAG handler for data sources. Receives pre-extracted search queries
/// from the coordinator, embeds them, searches the knowledge base index, and appends
/// relevant context to the system message.
/// </summary>
internal sealed class DataSourcePreemptiveRagHandler : IPreemptiveRagHandler
{
    private readonly ICatalog<AIDataSource> _dataSourceStore;
    private readonly IIndexProfileStore _indexProfileStore;
    private readonly IAIClientFactory _aiClientFactory;
    private readonly IAITemplateService _templateService;
    private readonly ISiteService _siteService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    public DataSourcePreemptiveRagHandler(
        ICatalog<AIDataSource> dataSourceStore,
        IIndexProfileStore indexProfileStore,
        IAIClientFactory aiClientFactory,
        IAITemplateService templateService,
        ISiteService siteService,
        IServiceProvider serviceProvider,
        ILogger<DataSourcePreemptiveRagHandler> logger)
    {
        _dataSourceStore = dataSourceStore;
        _indexProfileStore = indexProfileStore;
        _aiClientFactory = aiClientFactory;
        _templateService = templateService;
        _siteService = siteService;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public ValueTask<bool> CanHandleAsync(OrchestrationContextBuiltContext context)
    {
        if (context.OrchestrationContext.CompletionContext == null ||
            string.IsNullOrEmpty(context.OrchestrationContext.CompletionContext.DataSourceId))
        {
            return ValueTask.FromResult(false);
        }

        return ValueTask.FromResult(true);
    }

    public async Task HandleAsync(PreemptiveRagContext context)
    {
        var ragMetadata = GetRagMetadata(context.Resource);

        try
        {
            await InjectPreemptiveRagContextAsync(context, ragMetadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Preemptive RAG injection for data source '{DataSourceId}'.",
                context.OrchestrationContext.CompletionContext.DataSourceId);
        }
    }

    private async Task InjectPreemptiveRagContextAsync(PreemptiveRagContext context, AIDataSourceRagMetadata ragMetadata)
    {
        var orchestrationContext = context.OrchestrationContext;
        var dataSourceId = orchestrationContext.CompletionContext.DataSourceId;
        var dataSource = await _dataSourceStore.FindByIdAsync(dataSourceId);

        if (dataSource == null || string.IsNullOrEmpty(dataSource.AIKnowledgeBaseIndexProfileName))
        {
            return;
        }

        var masterProfile = await _indexProfileStore.FindByNameAsync(dataSource.AIKnowledgeBaseIndexProfileName);

        if (masterProfile == null)
        {
            return;
        }

        var contentManager = _serviceProvider.GetKeyedService<IDataSourceContentManager>(masterProfile.ProviderName);

        if (contentManager == null)
        {
            return;
        }

        // Get embedding configuration.
        var profileMetadata = masterProfile.As<DataSourceIndexProfileMetadata>();

        if (string.IsNullOrEmpty(profileMetadata.EmbeddingProviderName) ||
            string.IsNullOrEmpty(profileMetadata.EmbeddingConnectionName) ||
            string.IsNullOrEmpty(profileMetadata.EmbeddingDeploymentName))
        {
            return;
        }

        var embeddingGenerator = await _aiClientFactory.CreateEmbeddingGeneratorAsync(
            profileMetadata.EmbeddingProviderName,
            profileMetadata.EmbeddingConnectionName,
            profileMetadata.EmbeddingDeploymentName);

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

        // Get RAG settings with defaults from site settings.
        var siteSettings = await _siteService.GetSettingsAsync<AIDataSourceSettings>();
        var topN = siteSettings.GetTopNDocuments(ragMetadata?.TopNDocuments);

        // Translate OData filter if provided.
        string providerFilter = null;

        if (!string.IsNullOrWhiteSpace(ragMetadata?.Filter))
        {
            var filterTranslator = _serviceProvider.GetKeyedService<IODataFilterTranslator>(masterProfile.ProviderName);

            if (filterTranslator != null)
            {
                providerFilter = filterTranslator.Translate(ragMetadata?.Filter);
            }
        }

        // Perform vector search for each embedding and combine results.
        var allResults = new List<DataSourceSearchResult>();
        var seenChunkIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var embedding in embeddings)
        {
            if (embedding?.Vector == null)
            {
                continue;
            }

            var results = await contentManager.SearchAsync(
                masterProfile,
                embedding.Vector.ToArray(),
                dataSourceId,
                topN,
                providerFilter);

            if (results == null)
            {
                continue;
            }

            foreach (var result in results)
            {
                // Deduplicate by reference and chunk index to avoid injecting the same chunk multiple times.
                var chunkKey = $"{result.ReferenceId}:{result.ChunkIndex}";

                if (seenChunkIds.Add(chunkKey))
                {
                    allResults.Add(result);
                }
            }
        }

        var query = allResults.AsEnumerable();

        var strictness = siteSettings.GetStrictness(ragMetadata?.Strictness);

        if (strictness > 0)
        {
            // Apply strictness threshold.

            var threshold = strictness / (float)AIDataSourceSettings.MaxStrictness;
            query = query.Where(r => r.Score >= threshold);
        }

        // Sort by score descending and take top N.
        var finalResults = query
            .OrderByDescending(r => r.Score)
            .Take(topN)
            .AsEnumerable();

        if (!finalResults.Any())
        {
            return;
        }

        // Build context injection.
        using var sb = ZString.CreateStringBuilder();

        var arguments = new Dictionary<string, object>();

        if (!orchestrationContext.DisableTools)
        {
            arguments["searchToolName"] = SystemToolNames.SearchDataSources;
        }

        var header = await _templateService.RenderAsync(AITemplateIds.DataSourceContextHeader, arguments);

        if (!string.IsNullOrEmpty(header))
        {
            sb.AppendLine();
            sb.AppendLine();
            sb.Append(header);
        }

        var invocationContext = AIInvocationScope.Current;
        var seenReferences = new Dictionary<string, (int Index, string Title, string ReferenceType)>(StringComparer.OrdinalIgnoreCase);

        foreach (var result in finalResults)
        {
            if (string.IsNullOrWhiteSpace(result.Content))
            {
                continue;
            }

            var hasReference = !string.IsNullOrEmpty(result.ReferenceId);

            if (hasReference && !seenReferences.ContainsKey(result.ReferenceId))
            {
                seenReferences[result.ReferenceId] = (invocationContext?.NextReferenceIndex() ?? seenReferences.Count + 1, RagTextNormalizer.NormalizeTitle(result.Title), result.ReferenceType);
            }

            var refIdx = hasReference && seenReferences.TryGetValue(result.ReferenceId, out var entry)
                ? entry.Index
                : invocationContext?.NextReferenceIndex() ?? seenReferences.Count + 1;

            sb.AppendLine("---");
            sb.Append("[doc:");
            sb.Append(refIdx);
            sb.Append("] ");
            sb.AppendLine(result.Content);
        }

        if (seenReferences.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("References:");

            foreach (var kvp in seenReferences)
            {
                sb.Append("[doc:");
                sb.Append(kvp.Value.Index);
                sb.Append("] = {ReferenceId: \"");
                sb.Append(kvp.Key);
                sb.Append('"');

                if (!string.IsNullOrWhiteSpace(kvp.Value.Title))
                {
                    sb.Append(", Title: \"");
                    sb.Append(kvp.Value.Title);
                    sb.Append('"');
                }

                sb.AppendLine("}");
            }

            // Store citation metadata on the orchestration context for downstream consumers.
            var citationMap = new Dictionary<string, AICompletionReference>();

            foreach (var kvp in seenReferences)
            {
                var template = $"[doc:{kvp.Value.Index}]";
                citationMap[template] = new AICompletionReference
                {
                    Text = string.IsNullOrWhiteSpace(kvp.Value.Title) ? template : kvp.Value.Title,
                    Title = kvp.Value.Title,
                    Index = kvp.Value.Index,
                    ReferenceId = kvp.Key,
                    ReferenceType = kvp.Value.ReferenceType,
                };
            }

            orchestrationContext.Properties["DataSourceReferences"] = citationMap;
        }

        orchestrationContext.SystemMessageBuilder.Append(sb);
    }

    private static AIDataSourceRagMetadata GetRagMetadata(object resource)
    {
        if (resource is AIProfile profile &&
            profile.TryGet<AIDataSourceRagMetadata>(out var ragMetadata))
        {
            return ragMetadata;
        }

        if (resource is ChatInteraction interaction &&
            interaction.TryGet<AIDataSourceRagMetadata>(out var interactionRagMetadata))
        {
            return interactionRagMetadata;
        }

        return null;
    }
}
