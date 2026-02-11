using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class DefaultPromptRouter : IPromptRouter
{
    private readonly IPromptIntentDetector _intentDetector;
    private readonly IAICompletionContextBuilder _aICompletionContextBuilder;
    private readonly IPromptProcessingStrategyProvider _strategyProvider;
    private readonly IEnumerable<IPreIntentCapabilityResolver> _capabilityResolvers;
    private readonly ILogger<DefaultPromptRouter> _logger;

    public DefaultPromptRouter(
        IPromptIntentDetector intentDetector,
        IAICompletionContextBuilder aICompletionContextBuilder,
        IPromptProcessingStrategyProvider strategyProvider,
        IEnumerable<IPreIntentCapabilityResolver> capabilityResolvers,
        ILogger<DefaultPromptRouter> logger)
    {
        _intentDetector = intentDetector;
        _aICompletionContextBuilder = aICompletionContextBuilder;
        _strategyProvider = strategyProvider;
        _capabilityResolvers = capabilityResolvers;
        _logger = logger;
    }

    public async Task<IntentProcessingResult> RouteAsync(PromptRoutingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            // Build the completion context early so it's available for pre-intent resolution.
            var completionContext = await _aICompletionContextBuilder.BuildAsync(context.CompletionResource);

            // Run pre-intent capability resolution before intent detection.
            await ResolveCapabilitiesAsync(context, completionContext, cancellationToken);

            var intent = await _intentDetector.DetectAsync(context, cancellationToken);

            if (intent == null)
            {
                intent = DocumentIntent.FromName(
                    DocumentIntents.GeneralChatWithReference,
                    0.5f,
                    "No intent detected.");
            }

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Detected intent: {Intent} with confidence {Confidence}. Reason: {Reason}",
                    intent.Name, intent.Confidence, intent.Reason);
            }

            var processingContext = new IntentProcessingContext
            {
                Prompt = context.Prompt,
                Source = context.Source,
                CompletionContext = completionContext,
                DocumentInfos = context.Documents ?? [],
                ConversationHistory = context.ConversationHistory ?? [],
                PreIntentResolution = context.PreIntentResolution,
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

    private async Task ResolveCapabilitiesAsync(
        PromptRoutingContext context,
        AICompletionContext completionContext,
        CancellationToken cancellationToken)
    {
        foreach (var resolver in _capabilityResolvers)
        {
            try
            {
                var result = await resolver.ResolveAsync(context, completionContext, cancellationToken);

                if (result is not null && result.HasRelevantCapabilities)
                {
                    context.PreIntentResolution = result;

                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug(
                            "Pre-intent resolution found {Count} relevant capability candidate(s) from {Sources} source(s).",
                            result.Candidates.Count, result.RelevantSourceIds.Count);
                    }

                    // Use the first resolver that returns results.
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Pre-intent capability resolver {ResolverType} failed. Continuing without pre-resolved capabilities.", resolver.GetType().Name);
            }
        }
    }
}
