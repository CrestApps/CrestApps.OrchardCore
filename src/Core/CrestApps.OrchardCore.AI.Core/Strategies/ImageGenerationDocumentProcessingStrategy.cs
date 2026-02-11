using System.Text;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Core.Strategies;

/// <summary>
/// Strategy for handling image generation requests.
/// Uses AI image generation models to create visual content based on text descriptions.
/// </summary>
public sealed class ImageGenerationDocumentProcessingStrategy : DocumentProcessingStrategyBase
{
    private readonly AIProviderOptions _providerOptions;
    private readonly IAIClientFactory _aIClientFactory;
    private readonly ILogger<ImageGenerationDocumentProcessingStrategy> _logger;

    public ImageGenerationDocumentProcessingStrategy(
        IAIClientFactory aIClientFactory,
        IOptions<AIProviderOptions> providerOptions,
        ILogger<ImageGenerationDocumentProcessingStrategy> logger)
    {
        _providerOptions = providerOptions.Value;
        _aIClientFactory = aIClientFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task ProcessAsync(IntentProcessingContext context, CancellationToken cancellationToken = default)
    {
        var isGenerateImage = CanHandle(context, DocumentIntents.GenerateImage);
        var isGenerateImageWithHistory = CanHandle(context, DocumentIntents.GenerateImageWithHistory);

        if (!isGenerateImage && !isGenerateImageWithHistory)
        {
            return;
        }

        // Mark this as an image generation intent
        context.Result.IsImageGenerationIntent = true;

        var completionContext = context.CompletionContext;
        if (completionContext == null)
        {
            _logger.LogWarning("Completion context is not available, cannot generate images.");
            context.Result.SetFailed("Completion context is not available for image generation.");
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

            var providerName = context.Source;
            var connectionName = completionContext.ConnectionName;

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

            // Get the image deployment name
            var deploymentName = connection.GetDefaultImagesDeploymentName(throwException: false);
            if (string.IsNullOrEmpty(deploymentName))
            {
                _logger.LogWarning("No default images deployment name configured for connection '{ConnectionName}'.", connectionName);
                context.Result.SetFailed("No image generation model is configured. Please configure DefaultImagesDeploymentName in the connection settings.");
                return;
            }

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Generating image using provider '{ProviderName}', connection '{ConnectionName}', deployment '{DeploymentName}'.",
                    providerName, connectionName, deploymentName);
            }

            var imageGenerator = await _aIClientFactory.CreateImageGeneratorAsync(providerName, connectionName, deploymentName);

#pragma warning disable MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            var options = new ImageGenerationOptions
            {
                // Default to 1024x1024 for DALL-E 3
                ImageSize = new System.Drawing.Size(1024, 1024),
                ResponseFormat = ImageGenerationResponseFormat.Uri,
            };

            var request = new ImageGenerationRequest
            {
                Prompt = isGenerateImageWithHistory
                    ? BuildPromptWithHistory(context.Prompt, context.ConversationHistory, context.MaxHistoryMessagesForImageGeneration)
                    : context.Prompt,
            };
#pragma warning restore MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

            var result = await imageGenerator.GenerateAsync(request, options, cancellationToken);

            context.Result.GeneratedImages = result;

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Successfully generated {Count} image(s).", result.Contents?.Count ?? 0);
            }
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning(ex, "Image generation is not supported by the configured provider.");
            context.Result.SetFailed("Image generation is not supported by the configured AI provider.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during image generation.");
            context.Result.SetFailed($"An error occurred while generating the image: {ex.Message}");
        }
    }

    private const int DefaultMaxHistoryMessages = 5;
    private const int MaxMessageChars = 2000;

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
        builder.AppendLine("Conversation context (most recent messages):");
        builder.AppendLine();

        foreach (var msg in recent)
        {
            var text = msg.Text ?? string.Empty;
            if (text.Length > MaxMessageChars)
            {
                text = string.Concat(text.AsSpan(0, MaxMessageChars), "... [truncated]");
            }

            builder.Append(msg.Role.Value);
            builder.AppendLine(":");
            builder.AppendLine(text);
            builder.AppendLine();
        }

        builder.AppendLine("---");
        builder.AppendLine("Current request:");
        builder.AppendLine(currentPrompt);
        builder.AppendLine();
        builder.AppendLine("Generate an image that satisfies the current request using the conversation context when relevant.");

        return builder.ToString();
    }
}
