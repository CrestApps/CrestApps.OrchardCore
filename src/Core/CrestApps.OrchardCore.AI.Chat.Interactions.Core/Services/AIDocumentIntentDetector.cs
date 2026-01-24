using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core.Services;

/// <summary>
/// AI-based implementation of <see cref="IDocumentIntentDetector"/> that uses a chat model
/// to detect user intent. This provides multi-language support and more accurate intent
/// detection compared to keyword-based approaches.
/// </summary>
public sealed class AIDocumentIntentDetector : IDocumentIntentDetector
{
    private readonly AIProviderOptions _aiProviderOptions;
    private readonly IAIClientFactory _aIClientFactory;
    private readonly KeywordDocumentIntentDetector _fallbackDetector;
    private readonly IOptions<DocumentProcessingOptions> _options;
    private readonly ILogger<AIDocumentIntentDetector> _logger;

    public AIDocumentIntentDetector(
        IOptions<AIProviderOptions> aiProviderOptions,
        IAIClientFactory aIClientFactory,
        KeywordDocumentIntentDetector fallbackDetector,
        IOptions<DocumentProcessingOptions> options,
        ILogger<AIDocumentIntentDetector> logger)
    {
        _aiProviderOptions = aiProviderOptions.Value;
        _aIClientFactory = aIClientFactory;
        _fallbackDetector = fallbackDetector;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<DocumentIntent> DetectAsync(DocumentIntentDetectionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrWhiteSpace(context.Prompt))
        {
            return DocumentIntent.FromName(
                DocumentIntents.GeneralChatWithReference,
                0.5f,
                "No prompt provided, defaulting to general chat.");
        }

        // Try AI-based detection first
        var aiResult = await TryDetectWithAIAsync(context);
        if (aiResult != null)
        {
            return aiResult;
        }

        // Fall back to keyword-based detection
        _logger.LogDebug("AI-based intent detection unavailable, falling back to keyword-based detection.");
        var fallback = await _fallbackDetector.DetectAsync(context);

        return fallback;
    }

    private async Task<DocumentIntent> TryDetectWithAIAsync(DocumentIntentDetectionContext context)
    {
        var interaction = context.Interaction;
        if (interaction == null)
        {
            _logger.LogDebug("Interaction not available in context, cannot use AI-based intent detection.");
            return null;
        }

        // Check if we have any registered intents
        var intents = _options.Value.InternalIntents;
        if (intents.Count == 0)
        {
            _logger.LogDebug("No intents registered in DocumentProcessingOptions, cannot use AI-based intent detection.");
            return null;
        }

        try
        {
            if (_aiProviderOptions.Providers == null)
            {
                _logger.LogDebug("AI provider options not available.");
                return null;
            }

            var providerName = interaction.Source;
            var connectionName = interaction.ConnectionName;

            if (!_aiProviderOptions.Providers.TryGetValue(providerName, out var provider))
            {
                _logger.LogDebug("Provider '{ProviderName}' not found in configuration.", providerName);
                return null;
            }

            if (string.IsNullOrEmpty(connectionName))
            {
                connectionName = provider.DefaultConnectionName;
            }

            if (string.IsNullOrEmpty(connectionName) || !provider.Connections.TryGetValue(connectionName, out var connection))
            {
                _logger.LogDebug("Connection '{ConnectionName}' not found for provider '{ProviderName}'.", connectionName, providerName);
                return null;
            }

            // Try to get the intent-specific deployment, otherwise fall back to the default chat deployment
            var deploymentName = connection.GetDefaultIntentDeploymentName(throwException: false);
            if (string.IsNullOrEmpty(deploymentName))
            {
                deploymentName = connection.GetDefaultDeploymentName(throwException: false);
            }

            if (string.IsNullOrEmpty(deploymentName))
            {
                _logger.LogDebug("No deployment name configured for intent detection or chat.");
                return null;
            }

            var chatClient = await _aIClientFactory.CreateChatClientAsync(providerName, connectionName, deploymentName);
            if (chatClient == null)
            {
                _logger.LogDebug("Failed to create chat client for intent detection.");
                return null;
            }

            // Build the dynamic system prompt based on registered intents
            var systemPrompt = BuildSystemPrompt(intents);

            // Build the intent detection request
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, systemPrompt),
                new(ChatRole.User, BuildUserMessage(context)),
            };

            var options = new ChatOptions
            {
                Temperature = 0.1f, // Low temperature for consistent classification
                MaxOutputTokens = 200, // Short response expected
            };

            var response = await chatClient.GetResponseAsync(messages, options, context.CancellationToken);

            if (response == null || string.IsNullOrWhiteSpace(response.Text))
            {
                _logger.LogDebug("Empty response from AI for intent detection.");
                return null;
            }

            return ParseIntentResponse(response.Text, intents);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during AI-based intent detection, will fall back to keyword-based detection.");
            return null;
        }
    }

    private static string BuildSystemPrompt(Dictionary<string, string> intents)
    {
        var builder = new StringBuilder();

        builder.AppendLine("You are an intent classifier for document-related user queries. Analyze the user's prompt and classify their intent into exactly one of these categories:");
        builder.AppendLine();

        foreach (var (intent, description) in intents)
        {
            builder.AppendLine($"- {intent}: {description}");
        }

        builder.AppendLine();
        builder.AppendLine("Respond with a JSON object containing:");
        builder.AppendLine("- \"intent\": The exact intent name from the list above");
        builder.AppendLine("- \"confidence\": A number between 0.0 and 1.0 indicating your confidence");
        builder.AppendLine("- \"reason\": A brief explanation (max 50 words)");
        builder.AppendLine();
        builder.AppendLine("Example response:");

        // Use the first registered intent for the example
        var firstIntent = intents.Keys.FirstOrDefault() ?? DocumentIntents.DocumentQnA;
        builder.AppendLine($$"""
            {
                "intent": "{{firstIntent}}",
                "confidence": 0.95,
                "reason": "User's request matches this intent."
            }
            """);

        return builder.ToString();
    }


    private static string BuildUserMessage(DocumentIntentDetectionContext context)
    {
        var documentInfo = string.Empty;

        if (context.Documents?.Count > 0)
        {
            var fileNames = context.Documents
                .Where(d => !string.IsNullOrEmpty(d.FileName))
                .Select(d => d.FileName)
                .ToList();

            if (fileNames.Count > 0)
            {
                documentInfo = $"\n\nAttached documents: {string.Join(", ", fileNames)}";
            }
        }

        return $"User prompt: {context.Prompt}{documentInfo}";
    }

    private DocumentIntent ParseIntentResponse(string responseText, Dictionary<string, string> registeredIntents)
    {
        try
        {
            // Try to extract JSON from the response
            var jsonStart = responseText.IndexOf('{');
            var jsonEnd = responseText.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonText = responseText[jsonStart..(jsonEnd + 1)];
                var parsed = JsonSerializer.Deserialize<IntentResponseDto>(jsonText, JSOptions.CaseInsensitive);

                if (parsed != null && !string.IsNullOrEmpty(parsed.Intent))
                {
                    // Validate the intent is a registered value
                    var validIntent = ValidateIntent(parsed.Intent, registeredIntents);
                    var confidence = Math.Clamp(parsed.Confidence, 0f, 1f);

                    _logger.LogDebug("AI detected intent: {Intent} with confidence {Confidence}. Reason: {Reason}",
                        validIntent, confidence, parsed.Reason);

                    return DocumentIntent.FromName(validIntent, confidence, parsed.Reason ?? "AI-based detection");
                }
            }

            _logger.LogDebug("Could not parse intent response: {Response}", responseText);
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogDebug(ex, "Failed to parse JSON from AI intent response: {Response}", responseText);
            return null;
        }
    }

    private static string ValidateIntent(string intent, Dictionary<string, string> registeredIntents)
    {
        // Check if the intent is registered (case-insensitive)
        if (registeredIntents.ContainsKey(intent))
        {
            // Return the properly-cased key from the dictionary
            foreach (var key in registeredIntents.Keys)
            {
                if (string.Equals(key, intent, StringComparison.OrdinalIgnoreCase))
                {
                    return key;
                }
            }
        }

        // Default to GeneralChatWithReference if unknown
        return DocumentIntents.GeneralChatWithReference;
    }

    private sealed class IntentResponseDto
    {
        public string Intent { get; set; }
        public float Confidence { get; set; }
        public string Reason { get; set; }
    }
}
