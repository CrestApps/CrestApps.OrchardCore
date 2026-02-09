using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class DefaultPromptRouter : IPromptRouter
{
    private readonly IPromptIntentDetector _intentDetector;
    private readonly IPromptProcessingStrategyProvider _strategyProvider;
    private readonly ILogger<DefaultPromptRouter> _logger;

    public DefaultPromptRouter(
        IPromptIntentDetector intentDetector,
        IPromptProcessingStrategyProvider strategyProvider,
        ILogger<DefaultPromptRouter> logger)
    {
        _intentDetector = intentDetector;
        _strategyProvider = strategyProvider;
        _logger = logger;
    }

    public async Task<IntentProcessingResult> RouteAsync(PromptRoutingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var intentContext = new DocumentIntentDetectionContext
            {
                Prompt = request.Prompt,
                Interaction = request.Interaction,
                CancellationToken = request.CancellationToken,
            };

            var intent = await _intentDetector.DetectAsync(intentContext);

            if (intent == null)
            {
                intent = DocumentIntent.FromName(
                    DocumentIntents.GeneralChatWithReference,
                    0.5f,
                    "No intent detected.");
            }

            _logger.LogDebug("Detected intent: {Intent} with confidence {Confidence}. Reason: {Reason}",
                intent.Name, intent.Confidence, intent.Reason);

            var processingContext = new IntentProcessingContext
            {
                Prompt = request.Prompt,
                Interaction = request.Interaction,
                ConversationHistory = request.ConversationHistory ?? [],
                CancellationToken = request.CancellationToken,
            };

            if (request.MaxHistoryMessagesForImageGeneration.HasValue)
            {
                processingContext.MaxHistoryMessagesForImageGeneration = request.MaxHistoryMessagesForImageGeneration.Value;
            }

            processingContext.Result.Intent = intent.Name;
            processingContext.Result.Confidence = intent.Confidence;
            processingContext.Result.Reason = intent.Reason;

            await _strategyProvider.ProcessAsync(processingContext);

            return processingContext.Result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during intent detection or processing.");
            return null;
        }
    }
}
