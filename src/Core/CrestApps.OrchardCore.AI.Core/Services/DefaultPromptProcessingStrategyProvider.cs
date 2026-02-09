using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Default implementation of <see cref="IPromptProcessingStrategyProvider"/> that routes
/// document processing through all registered strategies, allowing multiple to contribute context.
/// </summary>
/// <remarks>
/// Heavy strategies (implementing <see cref="IHeavyPromptProcessingStrategy"/>) are only executed
/// when <see cref="PromptProcessingOptions.EnableHeavyProcessingStrategies"/> is true.
/// </remarks>
public sealed class DefaultPromptProcessingStrategyProvider : IPromptProcessingStrategyProvider
{
    private readonly IEnumerable<IPromptProcessingStrategy> _strategies;
    private readonly PromptProcessingOptions _options;
    private readonly ILogger _logger;

    public DefaultPromptProcessingStrategyProvider(
        IEnumerable<IPromptProcessingStrategy> strategies,
        IOptions<PromptProcessingOptions> options,
        ILogger<DefaultPromptProcessingStrategyProvider> logger)
    {
        _strategies = strategies;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task ProcessAsync(IntentProcessingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var intent = context.Result.Intent;
        ArgumentException.ThrowIfNullOrEmpty(intent);

        var isDebugging = _logger.IsEnabled(LogLevel.Debug);
        var enableHeavyStrategies = _options.EnableHeavyProcessingStrategies;

        // Call all strategies, allowing each to contribute context
        foreach (var strategy in _strategies)
        {
            // Skip heavy strategies if not enabled
            if (strategy is IHeavyPromptProcessingStrategy && !enableHeavyStrategies)
            {
                if (isDebugging)
                {
                    _logger.LogDebug(
                        "Skipping heavy strategy {StrategyType} for intent {Intent} because EnableHeavyProcessingStrategies is false.",
                        strategy.GetType().Name, intent);
                }
                continue;
            }

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
