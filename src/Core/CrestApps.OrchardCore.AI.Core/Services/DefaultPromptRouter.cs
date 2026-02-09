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

    public async Task<IntentProcessingResult> RouteAsync(PromptRoutingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            var intent = await _intentDetector.DetectAsync(context, cancellationToken);

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
                Prompt = context.Prompt,
                Source = context.Source,
                CompletionContext = context.CompletionContext,
                DocumentInfos = context.Documents ?? [],
                ConversationHistory = context.ConversationHistory ?? [],
            };

            if (context.MaxHistoryMessagesForImageGeneration.HasValue)
            {
                processingContext.MaxHistoryMessagesForImageGeneration = context.MaxHistoryMessagesForImageGeneration.Value;
            }

            processingContext.Result.Intent = intent.Name;
            processingContext.Result.Confidence = intent.Confidence;
            processingContext.Result.Reason = intent.Reason;

            await _strategyProvider.ProcessAsync(processingContext, cancellationToken);

            return processingContext.Result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during intent detection or processing.");
            return null;
        }
    }
}
