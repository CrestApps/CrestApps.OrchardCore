using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Entities;
using OrchardCore.Indexing;

namespace CrestApps.OrchardCore.AI.Tools;

/// <summary>
/// System tool that performs RAG (Retrieval-Augmented Generation) search
/// against data source embeddings and returns the most relevant chunks with citations.
/// </summary>
public sealed class DataSourceSearchTool : AIFunction
{
    public const string TheName = SystemToolNames.SearchDataSources;

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
        """
        {
          "type": "object",
          "properties": {
            "query": {
              "type": "string",
              "description": "The search query to find relevant content in the data source."
            }
          },
          "required": ["query"],
          "additionalProperties": false
        }
        """);

    public override string Name => TheName;

    public override string Description => "Searches configured data sources using semantic vector search and returns the most relevant text chunks with citations. Use this tool to answer questions based on the configured data source.";

    public override JsonElement JsonSchema => _jsonSchema;

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } =
        new Dictionary<string, object>() { ["Strict"] = false };

    protected override async ValueTask<object> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        if (!arguments.TryGetFirstString("query", out var query))
        {
            return "Unable to find a 'query' argument in the arguments parameter.";
        }

        var logger = arguments.Services.GetService<ILogger<DataSourceSearchTool>>();

        try
        {
            var httpContextAccessor = arguments.Services.GetService<IHttpContextAccessor>();
            var executionContext = httpContextAccessor?.HttpContext?.Items[nameof(AIToolExecutionContext)] as AIToolExecutionContext;

            if (executionContext == null)
            {
                return "Data source search requires an active AI execution context.";
            }

            // Get the data source ID from the completion context.
            var dataSourceId = httpContextAccessor?.HttpContext?.Items["DataSourceId"] as string;

            if (string.IsNullOrEmpty(dataSourceId))
            {
                return "No data source is configured for this profile.";
            }

            // Look up the data source to find its master index.
            var dataSourceStore = arguments.Services.GetRequiredService<ICatalog<AIDataSource>>();
            var dataSource = await dataSourceStore.FindByIdAsync(dataSourceId);

            if (dataSource == null)
            {
                return $"Data source '{dataSourceId}' was not found.";
            }

            var indexMetadata = dataSource.As<AIDataSourceIndexMetadata>();

            if (string.IsNullOrEmpty(indexMetadata.MasterIndexName))
            {
                return "No master embedding index is configured for this data source. Please configure an embedding index in the data source settings.";
            }

            // Resolve the master index profile.
            var indexProfileStore = arguments.Services.GetRequiredService<IIndexProfileStore>();
            var masterIndexProfile = await indexProfileStore.FindByNameAsync(indexMetadata.MasterIndexName);

            if (masterIndexProfile == null)
            {
                return $"Master embedding index '{indexMetadata.MasterIndexName}' was not found. Please create the index using the Indexing feature.";
            }

            // Get the vector search service for this provider.
            var searchService = arguments.Services.GetKeyedService<IDataSourceVectorSearchService>(masterIndexProfile.ProviderName);

            if (searchService == null)
            {
                return $"No vector search service is available for provider '{masterIndexProfile.ProviderName}'.";
            }

            // Resolve the embedding generator from the master index profile's configuration.
            var aiClientFactory = arguments.Services.GetRequiredService<IAIClientFactory>();

            var profileMetadata = masterIndexProfile.As<DataSourceIndexProfileMetadata>();

            if (string.IsNullOrEmpty(profileMetadata.EmbeddingProviderName) ||
                string.IsNullOrEmpty(profileMetadata.EmbeddingConnectionName) ||
                string.IsNullOrEmpty(profileMetadata.EmbeddingDeploymentName))
            {
                return "Embedding configuration is missing for the master embedding index.";
            }

            var embeddingGenerator = await aiClientFactory.CreateEmbeddingGeneratorAsync(
                profileMetadata.EmbeddingProviderName,
                profileMetadata.EmbeddingConnectionName,
                profileMetadata.EmbeddingDeploymentName);

            if (embeddingGenerator == null)
            {
                return "Failed to create embedding generator for data source search.";
            }

            // Generate embedding for the query.
            var embeddings = await embeddingGenerator.GenerateAsync([query], cancellationToken: cancellationToken);

            if (embeddings == null || embeddings.Count == 0 || embeddings[0]?.Vector == null)
            {
                return "Failed to generate embedding for the search query.";
            }

            // Get RAG settings from the profile if available.
            var ragMetadata = GetRagMetadata(executionContext);
            var topN = ragMetadata?.TopNDocuments ?? 5;

            if (topN <= 0)
            {
                topN = 5;
            }

            // Two-phase search: if filter is provided, first get matching reference IDs.
            IEnumerable<string> referenceIds = null;

            if (!string.IsNullOrWhiteSpace(ragMetadata?.Filter) && !string.IsNullOrEmpty(indexMetadata.IndexName))
            {
                // Resolve the source index profile to determine its provider.
                var sourceProfile = (await indexProfileStore.GetAllAsync())
                    .FirstOrDefault(i => string.Equals(i.Name, indexMetadata.IndexName, StringComparison.OrdinalIgnoreCase));

                if (sourceProfile != null)
                {
                    referenceIds = await ExecuteFilterQueryAsync(
                        arguments.Services,
                        sourceProfile.ProviderName,
                        indexMetadata.IndexName,
                        ragMetadata.Filter,
                        logger,
                        cancellationToken);
                }

                if (referenceIds != null && !referenceIds.Any())
                {
                    return "No documents matched the configured filter criteria.";
                }
            }

            // Perform the vector search.
            var results = await searchService.SearchAsync(
                masterIndexProfile,
                embeddings[0].Vector.ToArray(),
                dataSourceId,
                topN,
                referenceIds,
                cancellationToken);

            if (results == null || !results.Any())
            {
                if (ragMetadata?.IsInScope == true)
                {
                    return "No relevant content was found in the data source for this query. The answer is not available in the configured data source.";
                }

                return "No relevant content was found in the data source for this query. Answer using your general knowledge instead.";
            }

            // Apply strictness threshold.
            if (ragMetadata?.Strictness > 0)
            {
                var threshold = ragMetadata.Strictness.Value / 5.0f;
                results = results.Where(r => r.Score >= threshold);

                if (!results.Any())
                {
                    if (ragMetadata.IsInScope)
                    {
                        return "No results met the strictness threshold. The answer is not available in the configured data source.";
                    }

                    return "No results met the strictness threshold. Answer using your general knowledge instead.";
                }
            }

            // Format results with citations.
            var builder = new StringBuilder();
            builder.AppendLine("Relevant content from data source:");

            var docIndex = 1;
            var seenReferences = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var result in results)
            {
                if (string.IsNullOrWhiteSpace(result.Text))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(result.ReferenceId) &&
                    !seenReferences.ContainsKey(result.ReferenceId))
                {
                    seenReferences[result.ReferenceId] = docIndex;
                }

                var refLabel = !string.IsNullOrEmpty(result.ReferenceId) && seenReferences.TryGetValue(result.ReferenceId, out var refIdx)
                    ? $"[doc:{refIdx}]"
                    : $"[doc:{docIndex}]";

                builder.AppendLine("---");

                if (!string.IsNullOrWhiteSpace(result.Title))
                {
                    builder.AppendLine($"{refLabel} Title: {result.Title}");
                }

                builder.AppendLine($"{refLabel} {result.Text}");
                docIndex++;
            }

            if (seenReferences.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("References:");

                foreach (var kvp in seenReferences)
                {
                    builder.AppendLine($"[doc:{kvp.Value}] = {kvp.Key}");
                }
            }

            return builder.ToString();
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error during data source search.");
            return "An error occurred while searching the data source.";
        }
    }

    private static AIDataSourceRagMetadata GetRagMetadata(AIToolExecutionContext executionContext)
    {
        if (executionContext.Resource is AIProfile profile &&
            profile.TryGet<AIDataSourceRagMetadata>(out var ragMetadata))
        {
            return ragMetadata;
        }

        return null;
    }

    private static async Task<IEnumerable<string>> ExecuteFilterQueryAsync(
        IServiceProvider services,
        string providerName,
        string indexName,
        string filter,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var filterExecutor = services.GetKeyedService<IDataSourceFilterExecutor>(providerName);

            if (filterExecutor == null)
            {
                logger?.LogWarning("No filter executor is available for provider '{ProviderName}'.", providerName);
                return null;
            }

            return await filterExecutor.ExecuteAsync(indexName, filter, cancellationToken);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error executing filter query against source index.");
            return null;
        }
    }
}
