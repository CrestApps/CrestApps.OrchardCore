using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core.Services;

/// <summary>
/// Default implementation of <see cref="IDocumentProcessingStrategyProvider"/> that routes
/// document processing through all registered strategies, allowing multiple to contribute context.
/// </summary>
public sealed class DefaultDocumentProcessingStrategyProvider : IDocumentProcessingStrategyProvider
{
    private readonly IEnumerable<IDocumentProcessingStrategy> _strategies;
    private readonly ILogger _logger;

    public DefaultDocumentProcessingStrategyProvider(
        IEnumerable<IDocumentProcessingStrategy> strategies,
        ILogger<DefaultDocumentProcessingStrategyProvider> logger)
    {
        _strategies = strategies;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task ProcessAsync(DocumentProcessingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.IntentResult);

        var intent = context.IntentResult.Intent;

        var isDebugging = _logger.IsEnabled(LogLevel.Debug);

        // Call all strategies, allowing each to contribute context
        foreach (var strategy in _strategies)
        {
            try
            {
                if (isDebugging)
                {
                    _logger.LogDebug("Calling strategy {StrategyType} for intent {Intent}.", strategy.GetType().Name, intent);
                }

                var contextCountBefore = context.Result.AdditionalContexts.Count;
                await strategy.ProcessAsync(context);
                var contextCountAfter = context.Result.AdditionalContexts.Count;

                if (isDebugging && contextCountAfter > contextCountBefore)
                {
                    _logger.LogDebug("Strategy {StrategyType} added {ContextCount} context(s) for intent {Intent}.",
                        strategy.GetType().Name, contextCountAfter - contextCountBefore, intent);
                }
            }
            catch (Exception ex)
            {
                // Continue to next strategy on error
                _logger.LogError(ex, "Error in strategy {StrategyType} for intent {Intent}.", strategy.GetType().Name, intent);
            }
        }

        if (!context.Result.HasContext)
        {
            if (isDebugging)
            {
                _logger.LogDebug("No strategy added context for intent {Intent}.", intent);
            }
        }
    }
}
