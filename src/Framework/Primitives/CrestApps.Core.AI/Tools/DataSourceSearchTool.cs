using System.Text.Json;
using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Orchestration;
using CrestApps.Core.AI.Services;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Infrastructure.Indexing;
using CrestApps.Core.Infrastructure.Indexing.DataSources;
using CrestApps.Core.Services;
using Cysharp.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.Core.AI.Tools;

/// <summary>
/// Performs vector search against the configured data source knowledge base and returns relevant chunks with citations.
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
        new Dictionary<string, object>
        {
            ["Strict"] = false,
        };

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var logger = arguments.Services.GetRequiredService<ILogger<DataSourceSearchTool>>();

        if (!arguments.TryGetFirstString("query", out var query))
        {
            logger.LogWarning("AI tool '{ToolName}' missing required argument 'query'.", Name);
            return "Unable to find a 'query' argument in the arguments parameter.";
        }

        try
        {
            var invocationContext = AIInvocationScope.Current;
            var textNormalizer = arguments.Services.GetRequiredService<IAITextNormalizer>();
            var executionContext = invocationContext?.ToolExecutionContext;

            if (executionContext == null)
            {
                logger.LogWarning("AI tool '{ToolName}' failed: no active AI execution context.", Name);
                return "Data source search requires an active AI execution context.";
            }

            var dataSourceId = invocationContext.DataSourceId;

            if (string.IsNullOrEmpty(dataSourceId))
            {
                logger.LogWarning("AI tool '{ToolName}' failed: no data source configured for this profile.", Name);

                return "No data source is configured for this profile.";
            }

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

            var indexProfileStore = arguments.Services.GetRequiredService<ISearchIndexProfileStore>();
            var masterIndexProfile = await indexProfileStore.FindByNameAsync(dataSource.AIKnowledgeBaseIndexProfileName);

            if (masterIndexProfile == null)
            {
                logger.LogWarning("AI tool '{ToolName}' failed: knowledge base index '{IndexProfileName}' was not found.", Name, dataSource.AIKnowledgeBaseIndexProfileName);
                return $"Knowledge base index '{dataSource.AIKnowledgeBaseIndexProfileName}' was not found.";
            }

            var contentManager = arguments.Services.GetKeyedService<IDataSourceContentManager>(masterIndexProfile.ProviderName);

            if (contentManager == null)
            {
                logger.LogWarning("AI tool '{ToolName}' failed: no vector search service for provider '{ProviderName}'.", Name, masterIndexProfile.ProviderName);

                return $"No vector search service is available for provider '{masterIndexProfile.ProviderName}'.";
            }

            var aiClientFactory = arguments.Services.GetRequiredService<IAIClientFactory>();
            var deploymentManager = arguments.Services.GetRequiredService<IAIDeploymentManager>();
            var profileMetadata = SearchIndexProfileEmbeddingMetadataAccessor.GetMetadata(masterIndexProfile);

            var embeddingGenerator = await EmbeddingDeploymentResolver.CreateEmbeddingGeneratorAsync(
                deploymentManager,
                aiClientFactory,
                profileMetadata,
                masterIndexProfile.EmbeddingDeploymentId);

            if (embeddingGenerator == null)
            {
                logger.LogWarning("AI tool '{ToolName}' failed: embedding configuration is missing for the knowledge base index.", Name);
                return "Embedding configuration is missing for the knowledge base index.";
            }

            var embeddings = await embeddingGenerator.GenerateAsync([query], cancellationToken: cancellationToken);

            if (embeddings == null || embeddings.Count == 0 || embeddings[0]?.Vector == null)
            {
                logger.LogWarning("AI tool '{ToolName}' failed: could not generate embedding for query.", Name);

                return "Failed to generate embedding for the search query.";
            }

            var ragMetadata = GetRagMetadata(executionContext);
            var siteSettings = arguments.Services.GetRequiredService<IOptions<AIDataSourceOptions>>().Value;

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
                    logger.LogWarning("No OData filter translator available for provider '{ProviderName}'. Filter will be ignored.", masterIndexProfile.ProviderName);
                }
            }

            var results = await contentManager.SearchAsync(
                masterIndexProfile,
                embeddings[0].Vector.ToArray(),

            dataSourceId,
            siteSettings.GetTopNDocuments(ragMetadata?.TopNDocuments),
            providerFilter,
            cancellationToken);

            if (results == null || !results.Any())
            {
                return ragMetadata?.IsInScope == true
                    ? "No relevant content was found in the data source for this query. The answer is not available in the configured data source."
                    : "No relevant content was found in the data source for this query. Answer using your general knowledge instead.";
            }

            var strictness = siteSettings.GetStrictness(ragMetadata?.Strictness);

            if (strictness > 0)
            {
                var threshold = strictness / (float)AIDataSourceOptions.MaxStrictness;
                results = results.Where(result => result.Score >= threshold);

                if (!results.Any())
                {
                    return ragMetadata?.IsInScope == true
                        ? "No results met the strictness threshold. The answer is not available in the configured data source."
                        : "No results met the strictness threshold. Answer using your general knowledge instead.";
                }
            }

            using var builder = ZString.CreateStringBuilder();
            builder.AppendLine("Relevant content from data source:");

            var seenReferences = new Dictionary<string, (int Index, string Title, string ReferenceType)>(StringComparer.OrdinalIgnoreCase);

            foreach (var result in results)
            {

                if (string.IsNullOrWhiteSpace(result.Content))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(result.ReferenceId) && !seenReferences.ContainsKey(result.ReferenceId))
                {
                    seenReferences[result.ReferenceId] = (invocationContext.NextReferenceIndex(), textNormalizer.NormalizeTitle(result.Title), result.ReferenceType);
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
        if (executionContext.Resource is AIProfile profile && profile.TryGet<AIDataSourceRagMetadata>(out var ragMetadata))
        {
            return ragMetadata;
        }

        return null;
    }
}
