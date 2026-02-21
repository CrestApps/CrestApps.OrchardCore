using System.Text.Json;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Extracts focused search queries from the user's message using a lightweight utility LLM call.
/// Results are cached per <see cref="OrchestrationContext"/> so that multiple preemptive RAG
/// consumers (e.g., DataSource and Document handlers) share a single LLM call.
/// </summary>
public sealed class PreemptiveSearchQueryProvider
{
    /// <summary>
    /// The property key where the extracted search queries are cached in
    /// <see cref="OrchestrationContext.Properties"/>.
    /// </summary>
    private const string CacheKey = "PreemptiveSearchQueries";

    /// <summary>
    /// The maximum number of recent conversation messages (user + assistant) to include
    /// when extracting search queries, so follow-up messages are resolved correctly.
    /// </summary>
    private const int MaxConversationHistoryMessages = 4;

    private const string SearchQueryExtractionPrompt = """
        You are a search query extraction assistant. Given a conversation, extract 1 to 3 short,
        focused search queries that capture the key information needs of the latest user message.
        Each query should be a concise phrase suitable for semantic search against a knowledge base.

        Rules:
        1. Return ONLY a JSON array of strings. Example: ["query one", "query two"]
        2. Do NOT include any explanation, markdown, or formatting outside the JSON array.
        3. Strip out pleasantries, filler words, and irrelevant context.
        4. Each query should be self-contained and specific.
        5. If the user message is already a clear, focused question, return it as a single-element array.
        6. Use prior conversation messages to resolve references like "that", "it", "the above", etc.
        """;

    private readonly IAIClientFactory _aiClientFactory;
    private readonly AIProviderOptions _providerOptions;
    private readonly ILogger _logger;

    public PreemptiveSearchQueryProvider(
        IAIClientFactory aiClientFactory,
        IOptions<AIProviderOptions> providerOptions,
        ILogger<PreemptiveSearchQueryProvider> logger)
    {
        _aiClientFactory = aiClientFactory;
        _providerOptions = providerOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Gets search queries for the given context. The queries are extracted from the user's
    /// message using a utility LLM call and cached in
    /// <see cref="OrchestrationContext.Properties"/> so subsequent calls return the cached result.
    /// </summary>
    public async Task<IList<string>> GetQueriesAsync(OrchestrationContext context)
    {
        // Return cached queries if already extracted for this context.
        if (context.Properties.TryGetValue(CacheKey, out var cached) &&
            cached is IList<string> cachedQueries)
        {
            return cachedQueries;
        }

        var queries = await ExtractSearchQueriesAsync(context);
        context.Properties[CacheKey] = queries;

        return queries;
    }

    private async Task<IList<string>> ExtractSearchQueriesAsync(OrchestrationContext context)
    {
        var chatClient = await TryCreateUtilityChatClientAsync(context);

        if (chatClient == null)
        {
            return [context.UserMessage];
        }

        try
        {
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, SearchQueryExtractionPrompt),
            };

            // Include recent conversation history for context resolution.
            if (context.ConversationHistory is { Count: > 0 })
            {
                var recentMessages = context.ConversationHistory
                    .Where(m => m.Role == ChatRole.User || m.Role == ChatRole.Assistant)
                    .Where(m => !string.IsNullOrEmpty(m.Text))
                    .TakeLast(MaxConversationHistoryMessages);

                messages.AddRange(recentMessages);
            }

            // Ensure the current user message is the last message.
            if (messages.Count <= 1 || messages[^1].Text != context.UserMessage)
            {
                messages.Add(new ChatMessage(ChatRole.User, context.UserMessage));
            }

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

            var queries = ParseSearchQueries(response.Text);

            return queries.Count > 0 ? queries : [context.UserMessage];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract search queries using utility model. Falling back to raw user message.");

            return [context.UserMessage];
        }
    }

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

    private async Task<IChatClient> TryCreateUtilityChatClientAsync(OrchestrationContext context)
    {
        var providerName = context.SourceName;
        var connectionName = context.CompletionContext?.ConnectionName;

        if (string.IsNullOrEmpty(providerName) ||
            !_providerOptions.Providers.TryGetValue(providerName, out var provider))
        {
            return null;
        }

        if (string.IsNullOrEmpty(connectionName))
        {
            connectionName = provider.DefaultConnectionName;
        }

        if (string.IsNullOrEmpty(connectionName) ||
            !provider.Connections.TryGetValue(connectionName, out var connection))
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
}
