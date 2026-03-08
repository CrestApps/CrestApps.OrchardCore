using System.Text.Json;
using CrestApps.AI;
using CrestApps.AI.Extensions;
using CrestApps.AI.Models;
using CrestApps.AI.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.Services;
using Cysharp.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Entities;
using OrchardCore.Indexing;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.DataSources.Tools;

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
        new Dictionary<string, object>()
        {
            ["Strict"] = false,
        };

    protected override async ValueTask<object> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        var logger = arguments.Services.GetRequiredService<ILogger<DataSourceSearchTool>>();

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' invoked.", Name);
        }

        if (!arguments.TryGetFirstString("query", out var query))
        {
            logger.LogWarning("AI tool '{ToolName}' missing required argument 'query'.", Name);
            return "Unable to find a 'query' argument in the arguments parameter.";
        }

        try
        {
            var invocationContext = AIInvocationScope.Current;
            var executionContext = invocationContext?.ToolExecutionContext;

            if (executionContext == null)
            {
                logger.LogWarning("AI tool '{ToolName}' failed: no active AI execution context.", Name);
                return "Data source search requires an active AI execution context.";
            }

            // Get the data source ID from the invocation context.
            var dataSourceId = invocationContext.DataSourceId;

            if (string.IsNullOrEmpty(dataSourceId))
            {
                logger.LogWarning("AI tool '{ToolName}' failed: no data source configured for this profile.", Name);
                return "No data source is configured for this profile.";
            }

            // Look up the data source to find its master index.
            var dataSourceStore = arguments.Services.GetRequiredService<ICatalog<AIDataSource>>();
            var dataSource = await dataSourceStore.FindByIdAsync(dataSourceId);

            if (dataSource == null)
            {
                logger.LogWarning("AI tool '{ToolName}' failed: data source '{DataSourceId}' was not found.", Name, dataSourceId);
                return $"Data source '{dataSourceId}' was not found.";
            }

            if (string.IsNullOrEmpty(dataSource.AIKnowledgeBaseIndexProfileName))
            {
                logger.LogWarning("AI tool '{ToolName}' failed: no knowledge base index configured for data source '{DataSourceId}'.", Name, dataSourceId);
                return "No knowledge base index is configured for this data source. Please configure a knowledge base index in the data source settings.";
            }

            // Resolve the master index profile.
            var indexProfileStore = arguments.Services.GetRequiredService<IIndexProfileStore>();
            var masterIndexProfile = await indexProfileStore.FindByNameAsync(dataSource.AIKnowledgeBaseIndexProfileName);

            if (masterIndexProfile == null)
            {
                logger.LogWarning("AI tool '{ToolName}' failed: knowledge base index '{IndexProfileName}' was not found.", Name, dataSource.AIKnowledgeBaseIndexProfileName);
                return $"Knowledge base index '{dataSource.AIKnowledgeBaseIndexProfileName}' was not found. Please create the index using the Indexing feature.";
            }

            // Get the vector search service for this provider.
            var contentManager = arguments.Services.GetKeyedService<IDataSourceContentManager>(masterIndexProfile.ProviderName);

            if (contentManager == null)
            {
                logger.LogWarning("AI tool '{ToolName}' failed: no vector search service for provider '{ProviderName}'.", Name, masterIndexProfile.ProviderName);
                return $"No vector search service is available for provider '{masterIndexProfile.ProviderName}'.";
            }

            // Resolve the embedding generator from the master index profile's configuration.
            var aiClientFactory = arguments.Services.GetRequiredService<IAIClientFactory>();

            var profileMetadata = masterIndexProfile.As<DataSourceIndexProfileMetadata>();

            if (string.IsNullOrEmpty(profileMetadata.EmbeddingProviderName) ||
                string.IsNullOrEmpty(profileMetadata.EmbeddingConnectionName) ||
                string.IsNullOrEmpty(profileMetadata.EmbeddingDeploymentName))
            {
                logger.LogWarning("AI tool '{ToolName}' failed: embedding configuration is missing for the knowledge base index.", Name);
                return "Embedding configuration is missing for the knowledge base index.";
            }

            var embeddingGenerator = await aiClientFactory.CreateEmbeddingGeneratorAsync(
                profileMetadata.EmbeddingProviderName,
                profileMetadata.EmbeddingConnectionName,
                profileMetadata.EmbeddingDeploymentName);

            if (embeddingGenerator == null)
            {
                logger.LogWarning("AI tool '{ToolName}' failed: could not create embedding generator.", Name);
                return "Failed to create embedding generator for data source search.";
            }

            // Generate embedding for the query.
            var embeddings = await embeddingGenerator.GenerateAsync([query], cancellationToken: cancellationToken);

            if (embeddings == null || embeddings.Count == 0 || embeddings[0]?.Vector == null)
            {
                logger.LogWarning("AI tool '{ToolName}' failed: could not generate embedding for query.", Name);
                return "Failed to generate embedding for the search query.";
            }

            // Get RAG settings from the profile if available, with global defaults.
            var ragMetadata = GetRagMetadata(executionContext);

            var siteService = arguments.Services.GetRequiredService<ISiteService>();
            var siteSettings = await siteService.GetSettingsAsync<AIDataSourceSettings>();

            // Translate OData filter to provider-specific filter if provided.
            string providerFilter = null;

            if (!string.IsNullOrWhiteSpace(ragMetadata?.Filter))
            {
                var filterTranslator = arguments.Services.GetKeyedService<IODataFilterTranslator>(masterIndexProfile.ProviderName);

                if (filterTranslator != null)
                {
                    providerFilter = filterTranslator.Translate(ragMetadata.Filter);
                }
                else
                {
                    logger?.LogWarning("No OData filter translator available for provider '{ProviderName}'. Filter will be ignored.",
                        masterIndexProfile.ProviderName);
                }
            }

            // Perform the vector search with filter applied directly on the KB index.
            var results = await contentManager.SearchAsync(masterIndexProfile.ToIndexProfileInfo(),
                embeddings[0].Vector.ToArray(),
                dataSourceId,
                siteSettings.GetTopNDocuments(ragMetadata?.TopNDocuments),
                providerFilter,
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
            var strictness = siteSettings.GetStrictness(ragMetadata?.Strictness);

            if (strictness > 0)
            {
                var threshold = strictness / (float)AIDataSourceSettings.MaxStrictness;
                results = results.Where(r => r.Score >= threshold);

                if (!results.Any())
                {
                    if (ragMetadata?.IsInScope == true)
                    {
                        return "No results met the strictness threshold. The answer is not available in the configured data source.";
                    }

                    return "No results met the strictness threshold. Answer using your general knowledge instead.";
                }
            }

            // Format results with citations.
            var builder = ZString.CreateStringBuilder();
            builder.AppendLine("Relevant content from data source:");

            var seenReferences = new Dictionary<string, (int Index, string Title, string ReferenceType)>(StringComparer.OrdinalIgnoreCase);

            foreach (var result in results)
            {
                if (string.IsNullOrWhiteSpace(result.Content))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(result.ReferenceId) &&
                    !seenReferences.ContainsKey(result.ReferenceId))
                {
                    seenReferences[result.ReferenceId] = (invocationContext.NextReferenceIndex(), RagTextNormalizer.NormalizeTitle(result.Title), result.ReferenceType);
                }

                var refLabel = !string.IsNullOrEmpty(result.ReferenceId) && seenReferences.TryGetValue(result.ReferenceId, out var entry)
                    ? $"[doc:{entry.Index}]"
                    : $"[doc:{invocationContext.NextReferenceIndex()}]";

                builder.AppendLine("---");

                if (!string.IsNullOrWhiteSpace(result.Title))
                {
                    builder.Append(refLabel);
                    builder.Append(" Title: ");
                    builder.AppendLine(result.Title);
                }

                builder.Append(refLabel);
                builder.Append(' ');
                builder.AppendLine(result.Content);
            }

            if (seenReferences.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("References:");

                foreach (var kvp in seenReferences)
                {
                    builder.Append("[doc:");
                    builder.Append(kvp.Value.Index);
                    builder.Append("] = ");
                    builder.AppendLine(kvp.Key);
                }

                // Store citation metadata on the invocation context for downstream consumers.
                foreach (var kvp in seenReferences)
                {
                    var template = $"[doc:{kvp.Value.Index}]";
                    invocationContext.ToolReferences.TryAdd(template, new AICompletionReference
                    {
                        Text = string.IsNullOrWhiteSpace(kvp.Value.Title) ? template : kvp.Value.Title,
                        Title = kvp.Value.Title,
                        Index = kvp.Value.Index,
                        ReferenceId = kvp.Key,
                        ReferenceType = kvp.Value.ReferenceType,
                    });
                }
            }

            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("AI tool '{ToolName}' completed.", Name);
            }

            return builder.ToString();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during data source search.");
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
}
