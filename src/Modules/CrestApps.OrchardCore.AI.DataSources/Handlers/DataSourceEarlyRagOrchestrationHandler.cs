using System.Text;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Entities;
using OrchardCore.Indexing;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.DataSources.Handlers;

/// <summary>
/// Orchestration handler that performs Early RAG: embeds the user's query,
/// searches the knowledge base index, and appends relevant context to the
/// system message before the LLM call.
/// </summary>
internal sealed class DataSourceEarlyRagOrchestrationHandler : IOrchestrationContextBuilderHandler
{
    private readonly ICatalog<AIDataSource> _dataSourceStore;
    private readonly IIndexProfileStore _indexProfileStore;
    private readonly IAIClientFactory _aiClientFactory;
    private readonly ISiteService _siteService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    public DataSourceEarlyRagOrchestrationHandler(
        ICatalog<AIDataSource> dataSourceStore,
        IIndexProfileStore indexProfileStore,
        IAIClientFactory aiClientFactory,
        ISiteService siteService,
        IServiceProvider serviceProvider,
        ILogger<DataSourceEarlyRagOrchestrationHandler> logger)
    {
        _dataSourceStore = dataSourceStore;
        _indexProfileStore = indexProfileStore;
        _aiClientFactory = aiClientFactory;
        _siteService = siteService;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public Task BuildingAsync(OrchestrationContextBuildingContext context)
        => Task.CompletedTask;

    public async Task BuiltAsync(OrchestrationContextBuiltContext context)
    {
        if (context.Context.CompletionContext == null ||
            string.IsNullOrEmpty(context.Context.CompletionContext.DataSourceId) ||
            string.IsNullOrEmpty(context.Context.UserMessage))
        {
            return;
        }

        // Determine whether Early RAG should be used.
        var ragMetadata = GetRagMetadata(context.Resource);
        var isEarlyRagEnabled = await IsEarlyRagEnabledAsync(ragMetadata, context.Context);

        if (!isEarlyRagEnabled)
        {
            return;
        }

        try
        {
            await InjectEarlyRagContextAsync(context.Context, ragMetadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Early RAG injection for data source '{DataSourceId}'.",
                context.Context.CompletionContext.DataSourceId);
        }
    }

    private async Task<bool> IsEarlyRagEnabledAsync(AIDataSourceRagMetadata ragMetadata, OrchestrationContext context)
    {
        // If tools are disabled but a data source is configured, force Early RAG.
        if (context.DisableTools)
        {
            return true;
        }

        // Check per-profile setting first.
        if (ragMetadata?.EnableEarlyRag == true)
        {
            return ragMetadata.EnableEarlyRag;
        }

        // Fall back to global site setting.
        var siteSettings = await _siteService.GetSettingsAsync<AIDataSourceSettings>();

        return siteSettings.EnableEarlyRag;
    }

    private async Task InjectEarlyRagContextAsync(OrchestrationContext context, AIDataSourceRagMetadata ragMetadata)
    {
        var dataSourceId = context.CompletionContext.DataSourceId;
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

        // Generate embedding for the user query.
        var embeddings = await embeddingGenerator.GenerateAsync([context.UserMessage]);

        if (embeddings == null || embeddings.Count == 0 || embeddings[0]?.Vector == null)
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

        // Perform vector search.
        var results = await searchService.SearchAsync(
            masterProfile,
            embeddings[0].Vector.ToArray(),
            dataSourceId,
            topN,
            providerFilter);

        if (results == null || !results.Any())
        {
            if (ragMetadata?.IsInScope == true)
            {
                context.SystemMessageBuilder.AppendLine("\n\n[Data Source Context]");
                context.SystemMessageBuilder.AppendLine("No relevant content was found in the configured data source. Only answer based on the data source content. If no relevant content exists, inform the user.");
            }

            return;
        }

        // Apply strictness threshold.
        if (strictness > 0)
        {
            var threshold = strictness / 5.0f;
            results = results.Where(r => r.Score >= threshold);

            if (!results.Any())
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

        if (!context.DisableTools)
        {
            sb.AppendLine($"If you need additional context or more relevant information, use the '{SystemToolNames.SearchDataSources}' tool to retrieve more documents from the data source.");
        }

        var docIndex = 1;
        var seenReferences = new Dictionary<string, (int Index, string Title)>(StringComparer.OrdinalIgnoreCase);

        foreach (var result in results)
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

        context.SystemMessageBuilder.Append(sb);
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
