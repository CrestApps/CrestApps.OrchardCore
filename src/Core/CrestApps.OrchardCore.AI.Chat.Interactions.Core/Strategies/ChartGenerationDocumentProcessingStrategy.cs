using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core.Strategies;

/// <summary>
/// Strategy for handling chart generation requests.
/// Uses AI to generate Chart.js configuration from data in the conversation.
/// The AI model already receives conversation history, so this strategy always includes context.
/// </summary>
public sealed class ChartGenerationDocumentProcessingStrategy : DocumentProcessingStrategyBase
{
    private readonly AIProviderOptions _providerOptions;
    private readonly IAIClientFactory _aIClientFactory;
    private readonly ILogger<ChartGenerationDocumentProcessingStrategy> _logger;

    private const int DefaultMaxHistoryMessages = 10;
    private const int MaxMessageChars = 4000;

    private const string ChartGenerationSystemPrompt = """
        You are a data visualization expert. Your task is to generate Chart.js configuration JSON based on the user's request and data.

        IMPORTANT RULES:
        1. Return ONLY valid JSON that can be used directly with Chart.js
        2. Do NOT include any explanation, markdown, or code blocks - just the raw JSON
        3. The JSON must be a valid Chart.js configuration object with 'type', 'data', and optionally 'options' properties
        4. Supported chart types: 'bar', 'line', 'pie', 'doughnut', 'radar', 'polarArea', 'scatter', 'bubble'
        5. Use appropriate colors from this palette: ['#4dc9f6', '#f67019', '#f53794', '#537bc4', '#acc236', '#166a8f', '#00a950', '#58595b', '#8549ba']
        6. Include responsive: true and maintainAspectRatio: true in options
        7. Extract and structure data from the conversation to create meaningful visualizations

        Example output format:
        {"type":"bar","data":{"labels":["Jan","Feb","Mar"],"datasets":[{"label":"Sales","data":[10,20,30],"backgroundColor":["#4dc9f6","#f67019","#f53794"]}]},"options":{"responsive":true,"maintainAspectRatio":true}}
        """;

    public ChartGenerationDocumentProcessingStrategy(
        IAIClientFactory aIClientFactory,
        IOptions<AIProviderOptions> providerOptions,
        ILogger<ChartGenerationDocumentProcessingStrategy> logger)
    {
        _providerOptions = providerOptions.Value;
        _aIClientFactory = aIClientFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task ProcessAsync(IntentProcessingContext context)
    {
        if (!CanHandle(context, DocumentIntents.GenerateChart))
        {
            return;
        }

        // Mark this as a chart generation intent
        context.Result.IsChartGenerationIntent = true;

        var interaction = context.Interaction;
        if (interaction == null)
        {
            _logger.LogWarning("Interaction is not available in context, cannot generate charts.");
            context.Result.SetFailed("Interaction is not available for chart generation.");
            return;
        }

        try
        {
            if (_providerOptions.Providers == null)
            {
                _logger.LogWarning("AI provider options not available.");
                context.Result.SetFailed("AI provider configuration is not available.");
                return;
            }

            var providerName = interaction.Source;
            var connectionName = interaction.ConnectionName;

            if (!_providerOptions.Providers.TryGetValue(providerName, out var provider))
            {
                _logger.LogWarning("Provider '{ProviderName}' not found in configuration.", providerName);
                context.Result.SetFailed($"Provider '{providerName}' is not configured.");
                return;
            }

            if (string.IsNullOrEmpty(connectionName))
            {
                connectionName = provider.DefaultConnectionName;
            }

            if (string.IsNullOrEmpty(connectionName) || !provider.Connections.TryGetValue(connectionName, out var connection))
            {
                _logger.LogWarning("Connection '{ConnectionName}' not found for provider '{ProviderName}'.", connectionName, providerName);
                context.Result.SetFailed($"Connection '{connectionName}' is not configured for provider '{providerName}'.");
                return;
            }

            // Use the default chat deployment for chart generation
            var deploymentName = connection.GetDefaultDeploymentName(throwException: false);
            if (string.IsNullOrEmpty(deploymentName))
            {
                _logger.LogWarning("No default deployment name configured for connection '{ConnectionName}'.", connectionName);
                context.Result.SetFailed("No chat model is configured for chart generation.");
                return;
            }

            _logger.LogDebug("Generating chart using provider '{ProviderName}', connection '{ConnectionName}', deployment '{DeploymentName}'.",
                providerName, connectionName, deploymentName);

            var chatClient = await _aIClientFactory.CreateChatClientAsync(providerName, connectionName, deploymentName);

            // Build the prompt with conversation history for context
            // Always include history since chart requests often reference prior data
            var userPrompt = BuildPromptWithHistory(context.Prompt, context.ConversationHistory, context.MaxHistoryMessagesForImageGeneration);

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, ChartGenerationSystemPrompt),
                new(ChatRole.User, userPrompt),
            };

            var chatOptions = new ChatOptions
            {
                Temperature = 0.3f, // Lower temperature for more consistent JSON output
                MaxOutputTokens = 2000,
            };

            var response = await chatClient.GetResponseAsync(messages, chatOptions, context.CancellationToken);

            if (response == null || string.IsNullOrWhiteSpace(response.Text))
            {
                _logger.LogWarning("Empty response from AI for chart generation.");
                context.Result.SetFailed("Failed to generate chart configuration.");
                return;
            }

            // Extract and validate JSON from response
            var chartConfig = ExtractJsonFromResponse(response.Text);

            if (string.IsNullOrEmpty(chartConfig))
            {
                _logger.LogWarning("Could not extract valid JSON from AI response for chart generation.");
                context.Result.SetFailed("Failed to generate valid chart configuration.");
                return;
            }

            // Validate it's valid JSON
            try
            {
                using var doc = JsonDocument.Parse(chartConfig);
                // Check for required Chart.js properties
                if (!doc.RootElement.TryGetProperty("type", out _) || !doc.RootElement.TryGetProperty("data", out _))
                {
                    _logger.LogWarning("Generated JSON is missing required Chart.js properties.");
                    context.Result.SetFailed("Generated chart configuration is invalid.");
                    return;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Generated chart configuration is not valid JSON.");
                context.Result.SetFailed("Failed to generate valid chart configuration.");
                return;
            }

            context.Result.GeneratedChartConfig = chartConfig;

            _logger.LogDebug("Successfully generated chart configuration.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during chart generation.");
            context.Result.SetFailed($"An error occurred while generating the chart: {ex.Message}");
        }
    }

    private static string ExtractJsonFromResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return null;
        }

        var text = response.Trim();

        // If wrapped in markdown code blocks, extract the content
        if (text.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            var endIndex = text.IndexOf("```", 7);
            if (endIndex > 7)
            {
                text = text[7..endIndex].Trim();
            }
        }
        else if (text.StartsWith("```"))
        {
            var startIndex = text.IndexOf('\n');
            if (startIndex > 0)
            {
                var endIndex = text.LastIndexOf("```");
                if (endIndex > startIndex)
                {
                    text = text[(startIndex + 1)..endIndex].Trim();
                }
            }
        }

        // Find the first { and last } to extract JSON object
        var jsonStart = text.IndexOf('{');
        var jsonEnd = text.LastIndexOf('}');

        if (jsonStart >= 0 && jsonEnd > jsonStart)
        {
            return text[jsonStart..(jsonEnd + 1)];
        }

        return null;
    }

    private static string BuildPromptWithHistory(
        string prompt,
        IList<ChatMessage> conversationHistory,
        int maxHistoryMessages)
    {
        var currentPrompt = prompt?.Trim() ?? string.Empty;

        if (conversationHistory == null || conversationHistory.Count == 0)
        {
            return currentPrompt;
        }

        if (maxHistoryMessages <= 0)
        {
            maxHistoryMessages = DefaultMaxHistoryMessages;
        }

        var recent = conversationHistory.TakeLast(maxHistoryMessages);

        var builder = new StringBuilder();
        builder.AppendLine("Conversation context with data to visualize:");
        builder.AppendLine();

        foreach (var msg in recent)
        {
            var role = msg.Role == ChatRole.User ? "User" : "Assistant";

            var text = msg.Text ?? string.Empty;
            if (text.Length > MaxMessageChars)
            {
                text = string.Concat(text.AsSpan(0, MaxMessageChars), "... [truncated]");
            }

            builder.AppendLine($"{role}:");
            builder.AppendLine(text);
            builder.AppendLine();
        }

        builder.AppendLine("---");
        builder.AppendLine("Current request:");
        builder.AppendLine(currentPrompt);
        builder.AppendLine();
        builder.AppendLine("Generate a Chart.js configuration JSON that visualizes the data from the conversation above based on the current request.");

        return builder.ToString();
    }
}
