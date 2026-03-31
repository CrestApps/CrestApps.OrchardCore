using System.Text.Json;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Extensions;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using Cysharp.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Indexing;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Documents.Tools;

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
              "description": "The search query to find relevant content in available document knowledge."
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

    public override string Description => "Searches available document knowledge using semantic vector search and returns the most relevant text chunks. If no relevant content is found, you MUST still answer the user's prompt using your general knowledge.";

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
        var logger = arguments.Services.GetRequiredService<ILogger<SearchDocumentsTool>>();

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
            // Resolve the resource ID and type from the invocation context.
            var invocationContext = AIInvocationScope.Current;
            var executionContext = invocationContext?.ToolExecutionContext;

            // Collect all resource scopes to search across.
            var searchScopes = new List<(string ResourceId, string ReferenceType)>();

            if (executionContext?.Resource is ChatInteraction interaction)
            {
                searchScopes.Add((interaction.ItemId, AIConstants.DocumentReferenceTypes.ChatInteraction));
            }
            else if (executionContext?.Resource is AIProfile profile)
            {
                searchScopes.Add((profile.ItemId, AIConstants.DocumentReferenceTypes.Profile));

                // Also search session documents if a session with documents exists.
                if (invocationContext?.Items.TryGetValue(nameof(AIChatSession), out var sessionObj) == true &&
                    sessionObj is AIChatSession session &&
                    session.Documents is { Count: > 0 })
                {
                    searchScopes.Add((session.SessionId, AIConstants.DocumentReferenceTypes.ChatSession));
                }
            }

            if (searchScopes.Count == 0)
            {
                logger.LogWarning("AI tool '{ToolName}' failed: no active chat interaction session or AI profile.", Name);
                return "Document search requires an active chat interaction session or AI profile.";
            }

            var showUserDocumentAwareness =
                executionContext?.Resource is not AIProfile ||
                searchScopes.Any(scope => scope.ReferenceType == AIConstants.DocumentReferenceTypes.ChatSession);

            var siteService = arguments.Services.GetRequiredService<ISiteService>();
            var settings = await siteService.GetSettingsAsync<InteractionDocumentSettings>();

            if (string.IsNullOrEmpty(settings.IndexProfileName))
            {
                logger.LogWarning("AI tool '{ToolName}' failed: no index profile is configured.", Name);
                return "Document search is not configured. No index profile is set.";
            }

            var indexProfileStore = arguments.Services.GetRequiredService<IIndexProfileStore>();
            var indexProfile = await indexProfileStore.FindByNameAsync(settings.IndexProfileName);

            if (indexProfile is null)
            {
                logger.LogWarning("AI tool '{ToolName}' failed: index profile '{IndexProfileName}' was not found.", Name, settings.IndexProfileName);
                return $"Index profile '{settings.IndexProfileName}' was not found.";
            }

            var searchService = arguments.Services.GetKeyedService<IVectorSearchService>(indexProfile.ProviderName);

            if (searchService is null)
            {
                logger.LogWarning("AI tool '{ToolName}' failed: no search service available for provider '{ProviderName}'.", Name, indexProfile.ProviderName);
                return $"No search service is available for provider '{indexProfile.ProviderName}'.";
            }

            // Resolve the embedding deployment using the same execution context.
            var deploymentManager = arguments.Services.GetRequiredService<IAIDeploymentManager>();
            var aIClientFactory = arguments.Services.GetRequiredService<IAIClientFactory>();

            var providerName = executionContext?.ProviderName;
            var connectionName = executionContext?.ConnectionName;
            var deployment = await deploymentManager.ResolveOrDefaultAsync(
                AIDeploymentType.Embedding,
                clientName: providerName,
                connectionName: connectionName);

            if (deployment == null)
            {
                logger.LogWarning("AI tool '{ToolName}' failed: no embedding deployment configured.", Name);
                return "No embedding deployment is configured for document search.";
            }

            if (string.IsNullOrEmpty(deployment.ConnectionName))
            {
                logger.LogWarning("AI tool '{ToolName}' failed: embedding deployment '{DeploymentName}' has no connection reference.", Name, deployment.Name);
                return "The resolved embedding deployment does not define a connection.";
            }

            var embeddingGenerator = await aIClientFactory.CreateEmbeddingGeneratorAsync(
                deployment.ClientName,
                deployment.ConnectionName,
                deployment.ModelName);

            if (embeddingGenerator is null)
            {
                logger.LogWarning("AI tool '{ToolName}' failed: could not create embedding generator.", Name);
                return "Failed to create embedding generator for document search.";
            }

            var embeddings = await embeddingGenerator.GenerateAsync([query], cancellationToken: cancellationToken);

            if (embeddings is null || embeddings.Count == 0 || embeddings[0]?.Vector is null)
            {
                logger.LogWarning("AI tool '{ToolName}' failed: could not generate embedding for query.", Name);
                return "Failed to generate embedding for the search query.";
            }

            var topN = arguments.GetFirstValueOrDefault("top_n", settings.TopN);

            if (topN <= 0)
            {
                topN = 3;
            }

            // Search across all resource scopes and combine results.
            var allResults = new List<(DocumentChunkSearchResult Result, string ReferenceType)>();
            var seenChunkKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var hasProfileScope = searchScopes.Any(scope => scope.ReferenceType == AIConstants.DocumentReferenceTypes.Profile);
            var hasSessionScope = searchScopes.Any(scope => scope.ReferenceType == AIConstants.DocumentReferenceTypes.ChatSession);
            var keepProfileDocumentAwareness = !(executionContext?.Resource is AIProfile && hasProfileScope && hasSessionScope);

            foreach (var (scopeResourceId, scopeReferenceType) in searchScopes)
            {
                var results = await searchService.SearchAsync(
                    indexProfile,
                    embeddings[0].Vector.ToArray(),
                    scopeResourceId,
                    scopeReferenceType,
                    topN,
                    cancellationToken);

                if (results is null)
                {
                    continue;
                }

                foreach (var result in results)
                {
                    if (result.Chunk is null || string.IsNullOrWhiteSpace(result.Chunk.Text))
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

            // Sort by score descending and take top N.
            var finalResults = allResults
                .OrderByDescending(r => r.Result.Score)
                .Take(topN)
                .ToList();

            if (finalResults.Count == 0)
            {
                return showUserDocumentAwareness
                    ? "No relevant content was found in the uploaded documents for this query. Answer using your general knowledge instead."
                    : "No relevant background knowledge content was found for this query. Answer using your general knowledge instead.";
            }

            var builder = ZString.CreateStringBuilder();
            builder.AppendLine(showUserDocumentAwareness
                ? "Relevant content from uploaded documents:"
                : "Relevant background knowledge content:");

            if (showUserDocumentAwareness)
            {
                var seenDocuments = new Dictionary<string, (int Index, string FileName)>(StringComparer.OrdinalIgnoreCase);

                foreach (var (result, scopeReferenceType) in finalResults)
                {
                    if (result.Chunk is null || string.IsNullOrWhiteSpace(result.Chunk.Text))
                    {
                        continue;
                    }

                    if (!keepProfileDocumentAwareness && scopeReferenceType == AIConstants.DocumentReferenceTypes.Profile)
                    {
                        builder.AppendLine("---");
                        builder.AppendLine(result.Chunk.Text);
                        continue;
                    }

                    var documentKey = result.DocumentKey;

                    if (!string.IsNullOrEmpty(documentKey) && !seenDocuments.ContainsKey(documentKey))
                    {
                        seenDocuments[documentKey] = (invocationContext.NextReferenceIndex(), RagTextNormalizer.NormalizeTitle(result.FileName));
                    }

                    var refIdx = !string.IsNullOrEmpty(documentKey) && seenDocuments.TryGetValue(documentKey, out var entry)
                        ? entry.Index
                        : invocationContext.NextReferenceIndex();

                    builder.AppendLine("---");
                    builder.Append("[doc:");
                    builder.Append(refIdx);
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

                    // Store citation metadata on the invocation context for downstream consumers.
                    foreach (var kvp in seenDocuments)
                    {
                        var template = $"[doc:{kvp.Value.Index}]";
                        invocationContext.ToolReferences.TryAdd(template, new AICompletionReference
                        {
                            Text = string.IsNullOrWhiteSpace(kvp.Value.FileName) ? template : kvp.Value.FileName,
                            Title = kvp.Value.FileName,
                            Index = kvp.Value.Index,
                            ReferenceId = kvp.Key,
                            ReferenceType = AIConstants.DataSourceReferenceTypes.Document,
                        });
                    }
                }
            }
            else
            {
                foreach (var (result, _) in finalResults)
                {
                    if (result.Chunk is null || string.IsNullOrWhiteSpace(result.Chunk.Text))
                    {
                        continue;
                    }

                    builder.AppendLine("---");
                    builder.AppendLine(result.Chunk.Text);
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
            logger.LogError(ex, "Error during document search.");
            return "An error occurred while searching documents.";
        }
    }
}
