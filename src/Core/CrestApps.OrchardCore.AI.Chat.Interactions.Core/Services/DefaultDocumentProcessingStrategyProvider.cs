using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core.Services;

/// <summary>
/// Default implementation of <see cref="IDocumentProcessingStrategyProvider"/> that routes
/// document processing through all registered strategies until one handles the request.
/// </summary>
public sealed class DefaultDocumentProcessingStrategyProvider : IDocumentProcessingStrategyProvider
{
    private readonly IEnumerable<IDocumentProcessingStrategy> _strategies;
    private readonly ILogger<DefaultDocumentProcessingStrategyProvider> _logger;

    public DefaultDocumentProcessingStrategyProvider(
        IEnumerable<IDocumentProcessingStrategy> strategies,
        ILogger<DefaultDocumentProcessingStrategyProvider> logger)
    {
        _strategies = strategies;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<DocumentProcessingResult> ProcessAsync(DocumentProcessingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.IntentResult);

        var intent = context.IntentResult.Intent;

        // Try each strategy in sequence
        foreach (var strategy in _strategies)
        {
            try
            {
                _logger.LogDebug("Trying strategy {StrategyType} for intent {Intent}.",
                    strategy.GetType().Name, intent);

                var result = await strategy.ProcessAsync(context);

                if (result.Handled)
                {
                    _logger.LogDebug("Strategy {StrategyType} handled intent {Intent}. Success: {IsSuccess}.",
                        strategy.GetType().Name, intent, result.IsSuccess);
                    return result;
                }

                _logger.LogDebug("Strategy {StrategyType} did not handle intent {Intent}.",
                    strategy.GetType().Name, intent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in strategy {StrategyType} for intent {Intent}.",
                    strategy.GetType().Name, intent);
                // Continue to next strategy on error
            }
        }

        // No strategy handled the request
        _logger.LogWarning("No strategy handled intent {Intent}. Returning empty result.", intent);
        return DocumentProcessingResult.Empty();
    }
}
