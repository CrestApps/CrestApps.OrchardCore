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
/// AI-based implementation of <see cref="IPromptIntentDetector"/> that uses a chat model
/// to detect user intent. This provides multi-language support and more accurate intent
/// detection compared to keyword-based approaches.
/// </summary>
public sealed class AIPromptIntentDetector : IPromptIntentDetector
{
    private readonly AIProviderOptions _aiProviderOptions;
    private readonly IAIClientFactory _aIClientFactory;
    private readonly KeywordPromptIntentDetector _fallbackDetector;
    private readonly PromptProcessingOptions _processingOptions;
    private readonly ILogger<AIPromptIntentDetector> _logger;

    // Cached system prompt built once (options are immutable after startup).
    private string _cachedSystemPrompt;
    private bool _systemPromptBuilt;

    public AIPromptIntentDetector(
        IOptions<AIProviderOptions> aiProviderOptions,
        IAIClientFactory aIClientFactory,
        KeywordPromptIntentDetector fallbackDetector,
        IOptions<PromptProcessingOptions> processingOptions,
        ILogger<AIPromptIntentDetector> logger)
    {
        _aiProviderOptions = aiProviderOptions.Value;
        _aIClientFactory = aIClientFactory;
        _fallbackDetector = fallbackDetector;
        _processingOptions = processingOptions.Value;
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

        var systemPrompt = GetSystemPrompt();
        if (systemPrompt == null)
        {
            _logger.LogDebug("No intents available for AI-based intent detection.");
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

            return ParseIntentResponse(response.Text);
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

    private string GetSystemPrompt()
    {
        // Options are immutable after startup; build once.
        if (_systemPromptBuilt)
        {
            return _cachedSystemPrompt;
        }

        _cachedSystemPrompt = BuildSystemPrompt();
        _systemPromptBuilt = true;

        return _cachedSystemPrompt;
    }

    private string BuildSystemPrompt()
    {
        var allIntents = _processingOptions.InternalIntents;
        var heavyIntents = _processingOptions.HeavyIntents;
        var enableHeavy = _processingOptions.EnableHeavyProcessingStrategies;

        // Determine which intents to include.
        var hasIntents = false;
        var builder = new StringBuilder();

        builder.AppendLine("You are an intent classifier for document-related user queries. Analyze the user's prompt and classify their intent into exactly one of these categories:");
        builder.AppendLine();

        foreach (var (intent, description) in allIntents)
        {
            // Skip heavy intents when heavy processing is disabled.
            if (!enableHeavy && heavyIntents.Contains(intent))
            {
                continue;
            }

            builder.AppendLine($"- {intent}: {description}");
            hasIntents = true;
        }

        if (!hasIntents)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("No intents available for system prompt (all intents may be heavy and heavy processing is disabled).");
            }

            return null;
        }

        if (!enableHeavy && heavyIntents.Count > 0 && _logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Heavy processing disabled. Excluding heavy intents from AI detection: {Intents}",
                string.Join(", ", heavyIntents));
        }

        builder.AppendLine();
        builder.AppendLine("Respond with a JSON object containing:");
        builder.AppendLine("- \"intent\": The exact intent name from the list above");
        builder.AppendLine("- \"confidence\": A number between 0.0 and 1.0 indicating your confidence");
        builder.AppendLine("- \"reason\": A brief explanation (max 50 words)");
        builder.AppendLine();
        builder.AppendLine("Example response:");

        // Use the first available intent for the example.
        string firstIntent = null;
        foreach (var intent in allIntents.Keys)
        {
            if (enableHeavy || !heavyIntents.Contains(intent))
            {
                firstIntent = intent;
                break;
            }
        }

        firstIntent ??= DocumentIntents.DocumentQnA;

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

    private DocumentIntent ParseIntentResponse(string responseText)
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
                    var validIntent = ValidateIntent(parsed.Intent);
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

    private string ValidateIntent(string intent)
    {
        var allIntents = _processingOptions.InternalIntents;
        var heavyIntents = _processingOptions.HeavyIntents;
        var enableHeavy = _processingOptions.EnableHeavyProcessingStrategies;

        // Check if the intent is available (case-insensitive) and not filtered out.
        foreach (var key in allIntents.Keys)
        {
            if (string.Equals(key, intent, StringComparison.OrdinalIgnoreCase))
            {
                // If it's a heavy intent and heavy is disabled, reject it.
                if (!enableHeavy && heavyIntents.Contains(key))
                {
                    break;
                }

                return key;
            }
        }

        // Default to GeneralChatWithReference if unknown or not available.
        return DocumentIntents.GeneralChatWithReference;
    }

    private sealed class IntentResponseDto
    {
        public string Intent { get; set; }
        public float Confidence { get; set; }
        public string Reason { get; set; }
    }
}
