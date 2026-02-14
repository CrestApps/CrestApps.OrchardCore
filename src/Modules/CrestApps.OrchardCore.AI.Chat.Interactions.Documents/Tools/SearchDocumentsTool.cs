using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Extensions;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Indexing;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Documents.Tools;

/// <summary>
/// System tool that performs RAG (Retrieval-Augmented Generation) vector search
/// over uploaded documents and returns the most relevant chunks.
/// </summary>
public sealed class SearchDocumentsTool : AIFunction
{
    public const string TheName = SystemToolNames.SearchDocuments;

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
        """
        {
          "type": "object",
          "properties": {
            "query": {
              "type": "string",
              "description": "The search query to find relevant content in uploaded documents."
            },
            "top_n": {
              "type": "integer",
              "description": "Number of top matching chunks to return. Defaults to 3."
            }
          },
          "required": ["query"],
          "additionalProperties": false
        }
        """);

    public override string Name => TheName;

    public override string Description => "Searches uploaded documents using semantic vector search and returns the most relevant text chunks. This tool ONLY searches uploaded documents. If no relevant content is found, you MUST still answer the user's prompt using your general knowledge.";

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

        var logger = arguments.Services.GetService<ILogger<SearchDocumentsTool>>();

        try
        {
            // Resolve the chat interaction ID from the execution context resource.
            var httpContextAccessor = arguments.Services.GetService<IHttpContextAccessor>();
            var executionContext = httpContextAccessor?.HttpContext?.Items[nameof(AIToolExecutionContext)] as AIToolExecutionContext;

            if (executionContext?.Resource is not ChatInteraction interaction)
            {
                return "Document search requires an active chat interaction session.";
            }

            var chatInteractionId = interaction.ItemId;
            var siteService = arguments.Services.GetRequiredService<ISiteService>();
            var settings = await siteService.GetSettingsAsync<InteractionDocumentSettings>();

            if (string.IsNullOrEmpty(settings.IndexProfileName))
            {
                return "Document search is not configured. No index profile is set.";
            }

            var indexProfileStore = arguments.Services.GetRequiredService<IIndexProfileStore>();
            var indexProfile = await indexProfileStore.FindByNameAsync(settings.IndexProfileName);

            if (indexProfile is null)
            {
                return $"Index profile '{settings.IndexProfileName}' was not found.";
            }

            var searchService = arguments.Services.GetKeyedService<IVectorSearchService>(indexProfile.ProviderName);

            if (searchService is null)
            {
                return $"No search service is available for provider '{indexProfile.ProviderName}'.";
            }

            // Resolve the embedding deployment using the same execution context.
            var providerOptions = arguments.Services.GetRequiredService<IOptions<AIProviderOptions>>().Value;
            var aIClientFactory = arguments.Services.GetRequiredService<IAIClientFactory>();

            var providerName = executionContext?.ProviderName;
            var connectionName = executionContext?.ConnectionName;
            string deploymentName = null;

            if (!string.IsNullOrEmpty(providerName) &&
                providerOptions.Providers.TryGetValue(providerName, out var provider))
            {
                if (string.IsNullOrEmpty(connectionName))
                {
                    connectionName = provider.DefaultConnectionName;
                }

                if (!string.IsNullOrEmpty(connectionName) &&
                    provider.Connections.TryGetValue(connectionName, out var connection))
                {
                    deploymentName = connection.GetDefaultEmbeddingDeploymentName(false);
                }
            }

            if (string.IsNullOrEmpty(deploymentName))
            {
                return "No embedding deployment is configured for document search.";
            }

            var embeddingGenerator = await aIClientFactory.CreateEmbeddingGeneratorAsync(providerName, connectionName, deploymentName);

            if (embeddingGenerator is null)
            {
                return "Failed to create embedding generator for document search.";
            }

            var embeddings = await embeddingGenerator.GenerateAsync([query], cancellationToken: cancellationToken);

            if (embeddings is null || embeddings.Count == 0 || embeddings[0]?.Vector is null)
            {
                return "Failed to generate embedding for the search query.";
            }

            var topN = arguments.GetFirstValueOrDefault("top_n", settings.TopN);

            if (topN <= 0)
            {
                topN = 3;
            }

            var results = await searchService.SearchAsync(
                indexProfile,
                embeddings[0].Vector.ToArray(),
                chatInteractionId,
                topN,
                cancellationToken);

            if (results is null || !results.Any())
            {
                return "No relevant content was found in the uploaded documents for this query. Answer using your general knowledge instead.";
            }

            var builder = new StringBuilder();
            builder.AppendLine("Relevant content from uploaded documents:");

            foreach (var result in results)
            {
                if (result.Chunk is not null && !string.IsNullOrWhiteSpace(result.Chunk.Text))
                {
                    builder.AppendLine("---");
                    builder.AppendLine(result.Chunk.Text);
                }
            }

            return builder.ToString();
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error during document search.");
            return "An error occurred while searching documents.";
        }
    }
}
