using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Entities;
using OrchardCore.Indexing;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.DataSources.Handlers;

/// <summary>
/// Orchestration handler that performs Preemptive RAG: rewrites the user's query
/// into focused search terms, embeds them, searches the knowledge base index,
/// and appends relevant context to the system message before the LLM call.
/// </summary>
internal sealed class DataSourcePreemptiveRagOrchestrationHandler : IOrchestrationContextBuilderHandler
{
    private const string SearchQueryExtractionPrompt = """
        You are a search query extraction assistant. Given a user message, extract 1 to 3 short, 
        focused search queries that capture the key information needs. Each query should be a concise 
        phrase suitable for semantic search against a knowledge base.

        Rules:
        1. Return ONLY a JSON array of strings. Example: ["query one", "query two"]
        2. Do NOT include any explanation, markdown, or formatting outside the JSON array.
        3. Strip out pleasantries, filler words, and irrelevant context.
        4. Each query should be self-contained and specific.
        5. If the user message is already a clear, focused question, return it as a single-element array.
        """;

    private readonly ICatalog<AIDataSource> _dataSourceStore;
    private readonly IIndexProfileStore _indexProfileStore;
    private readonly IAIClientFactory _aiClientFactory;
    private readonly ISiteService _siteService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    public DataSourcePreemptiveRagOrchestrationHandler(
        ICatalog<AIDataSource> dataSourceStore,
        IIndexProfileStore indexProfileStore,
        IAIClientFactory aiClientFactory,
        ISiteService siteService,
        IServiceProvider serviceProvider,
        ILogger<DataSourcePreemptiveRagOrchestrationHandler> logger)
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

        // Determine whether Preemptive RAG should be used.
        var ragMetadata = GetRagMetadata(context.Resource);
        var isPreemptiveRagEnabled = await IsPreemptiveRagEnabledAsync(ragMetadata, context.Context);

        if (!isPreemptiveRagEnabled)
        {
            return;
        }

        try
        {
            await InjectPreemptiveRagContextAsync(context.Context, ragMetadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Preemptive RAG injection for data source '{DataSourceId}'.",
                context.Context.CompletionContext.DataSourceId);
        }
    }

    private async Task<bool> IsPreemptiveRagEnabledAsync(AIDataSourceRagMetadata ragMetadata, OrchestrationContext context)
    {
        // If tools are disabled but a data source is configured, force Preemptive RAG.
        if (context.DisableTools)
        {
            return true;
        }

        // Check per-profile setting first.
        if (ragMetadata?.EnablePreemptiveRag == true)
        {
            return ragMetadata.EnablePreemptiveRag;
        }

        // Fall back to global site setting.
        var siteSettings = await _siteService.GetSettingsAsync<AIDataSourceSettings>();

        return siteSettings.EnablePreemptiveRag;
    }

    private async Task InjectPreemptiveRagContextAsync(OrchestrationContext context, AIDataSourceRagMetadata ragMetadata)
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

        // Extract focused search queries from the user message using a utility model.
        var searchQueries = await ExtractSearchQueriesAsync(context);

        // Generate embeddings for all search queries.
        var embeddings = await embeddingGenerator.GenerateAsync(searchQueries);

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
                context.SystemMessageBuilder.AppendLine("\n\n[Data Source Context]");
                context.SystemMessageBuilder.AppendLine("No relevant content was found in the configured data source. Only answer based on the data source content. If no relevant content exists, inform the user.");
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

        if (!context.DisableTools)
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

        context.SystemMessageBuilder.Append(sb);
    }

    /// <summary>
    /// Uses a lightweight LLM call to extract focused search queries from the user's message.
    /// Falls back to the raw user message if no chat client is available.
    /// </summary>
    private async Task<IList<string>> ExtractSearchQueriesAsync(OrchestrationContext context)
    {
        var chatClient = await TryCreateUtilityChatClientAsync(context);

        if (chatClient == null)
        {
            // Fallback: use the raw user message as-is.
            return [context.UserMessage];
        }

        try
        {
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, SearchQueryExtractionPrompt),
                new(ChatRole.User, context.UserMessage),
            };

            var chatOptions = new ChatOptions
            {
                Temperature = 0.2f,
                MaxOutputTokens = 200,
            };

            var response = await chatClient.GetResponseAsync(messages, chatOptions);

            if (response == null || string.IsNullOrWhiteSpace(response.Text))
            {
                return [context.UserMessage];
            }

            // Parse the JSON array of search queries.
            var queries = ParseSearchQueries(response.Text);

            return queries.Count > 0 ? queries : [context.UserMessage];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract search queries using utility model. Falling back to raw user message.");

            return [context.UserMessage];
        }
    }

    /// <summary>
    /// Parses a JSON array of search query strings from the LLM response.
    /// </summary>
    private static List<string> ParseSearchQueries(string responseText)
    {
        var text = responseText.Trim();

        // Strip markdown code fences if the model wraps the JSON.
        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewLine = text.IndexOf('\n');

            if (firstNewLine > 0)
            {
                var lastFence = text.LastIndexOf("```", StringComparison.Ordinal);

                if (lastFence > firstNewLine)
                {
                    text = text[(firstNewLine + 1)..lastFence].Trim();
                }
            }
        }

        try
        {
            var queries = JsonSerializer.Deserialize<List<string>>(text);

            if (queries != null)
            {
                return queries
                    .Where(q => !string.IsNullOrWhiteSpace(q))
                    .ToList();
            }
        }
        catch (JsonException)
        {
            // Ignore parse errors; caller will fall back to the raw user message.
        }

        return [];
    }

    /// <summary>
    /// Attempts to create a chat client using the utility deployment name if configured,
    /// falling back to the default deployment name.
    /// </summary>
    private async Task<IChatClient> TryCreateUtilityChatClientAsync(OrchestrationContext context)
    {
        var providerName = context.SourceName;
        var connectionName = context.CompletionContext?.ConnectionName;

        if (string.IsNullOrEmpty(providerName))
        {
            return null;
        }

        var providerOptions = _serviceProvider.GetRequiredService<IOptions<AIProviderOptions>>().Value;

        if (!providerOptions.Providers.TryGetValue(providerName, out var provider))
        {
            return null;
        }

        if (string.IsNullOrEmpty(connectionName))
        {
            connectionName = provider.DefaultConnectionName;
        }

        if (string.IsNullOrEmpty(connectionName) || !provider.Connections.TryGetValue(connectionName, out var connection))
        {
            return null;
        }

        // Prefer the utility deployment, fall back to the default deployment.
        var deploymentName = connection.GetDefaultUtilityDeploymentName(throwException: false);

        if (string.IsNullOrEmpty(deploymentName))
        {
            deploymentName = connection.GetDefaultDeploymentName(throwException: false);
        }

        if (string.IsNullOrEmpty(deploymentName))
        {
            return null;
        }

        return await _aiClientFactory.CreateChatClientAsync(providerName, connectionName, deploymentName);
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
