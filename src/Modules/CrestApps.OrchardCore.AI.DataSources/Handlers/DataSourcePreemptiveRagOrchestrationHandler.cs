using System.Text;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.AI;
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
    private readonly ISiteService _siteService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    public DataSourcePreemptiveRagHandler(
        ICatalog<AIDataSource> dataSourceStore,
        IIndexProfileStore indexProfileStore,
        IAIClientFactory aiClientFactory,
        ISiteService siteService,
        IServiceProvider serviceProvider,
        ILogger<DataSourcePreemptiveRagHandler> logger)
    {
        _dataSourceStore = dataSourceStore;
        _indexProfileStore = indexProfileStore;
        _aiClientFactory = aiClientFactory;
        _siteService = siteService;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task HandleAsync(PreemptiveRagContext context)
    {
        if (context.OrchestrationContext.CompletionContext == null ||
            string.IsNullOrEmpty(context.OrchestrationContext.CompletionContext.DataSourceId))
        {
            return;
        }

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

        var searchService = _serviceProvider.GetKeyedService<IDataSourceVectorSearchService>(masterProfile.ProviderName);

        if (searchService == null)
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
        var strictness = siteSettings.GetStrictness(ragMetadata?.Strictness);

        // Translate OData filter if provided.
        string providerFilter = null;

        if (!string.IsNullOrWhiteSpace(ragMetadata?.Filter))
        {
            var filterTranslator = _serviceProvider.GetKeyedService<IODataFilterTranslator>(masterProfile.ProviderName);

            if (filterTranslator != null)
            {
                providerFilter = filterTranslator.Translate(ragMetadata.Filter);
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

            var results = await searchService.SearchAsync(
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

        // Sort by score descending and take top N.
        var finalResults = allResults
            .OrderByDescending(r => r.Score)
            .Take(topN)
            .AsEnumerable();

        if (!finalResults.Any())
        {
            if (ragMetadata?.IsInScope == true)
            {
                orchestrationContext.SystemMessageBuilder.AppendLine("\n\n[Data Source Context]");
                orchestrationContext.SystemMessageBuilder.AppendLine("No relevant content was found in the configured data source. Only answer based on the data source content. If no relevant content exists, inform the user.");
            }

            return;
        }

        // Apply strictness threshold.
        if (strictness > 0)
        {
            var threshold = strictness / 5.0f;
            finalResults = finalResults.Where(r => r.Score >= threshold);

            if (!finalResults.Any())
            {
                return;
            }
        }

        // Build context injection.
        var sb = new StringBuilder();
        sb.AppendLine("\n\n[Data Source Context]");
        sb.AppendLine("The following context was retrieved from the configured data source. Use this information to answer the user's question accurately and directly without mentioning or referencing the retrieval process.");

        if (ragMetadata?.IsInScope == true)
        {
            sb.AppendLine("IMPORTANT: Only answer based on the data source content. If the context does not contain relevant information, inform the user that the answer is not available in the data source.");
        }

        if (!orchestrationContext.DisableTools)
        {
            sb.AppendLine($"If you need additional context or more relevant information, use the '{SystemToolNames.SearchDataSources}' tool to retrieve more documents from the data source.");
        }

        var docIndex = 1;
        var seenReferences = new Dictionary<string, (int Index, string Title)>(StringComparer.OrdinalIgnoreCase);

        foreach (var result in finalResults)
        {
            if (string.IsNullOrWhiteSpace(result.Content))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(result.ReferenceId) &&
                !seenReferences.ContainsKey(result.ReferenceId))
            {
                seenReferences[result.ReferenceId] = (docIndex, result.Title);
            }

            var refIdx = !string.IsNullOrEmpty(result.ReferenceId) && seenReferences.TryGetValue(result.ReferenceId, out var entry)
                ? entry.Index
                : docIndex;

            sb.AppendLine("---");
            sb.AppendLine($"[doc:{refIdx}] {result.Content}");
            docIndex++;
        }

        if (seenReferences.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("References:");

            foreach (var kvp in seenReferences)
            {
                sb.Append($"[doc:{kvp.Value.Index}] = {{ReferenceId: \"{kvp.Key}\"");

                if (!string.IsNullOrWhiteSpace(kvp.Value.Title))
                {
                    sb.Append($", Title: \"{kvp.Value.Title}\"");
                }

                sb.AppendLine("}");
            }
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
