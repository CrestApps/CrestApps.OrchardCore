using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Default implementation of <see cref="IPromptProcessingStrategyProvider"/> that routes
/// document processing through all registered strategies, allowing multiple to contribute context.
/// </summary>
/// <remarks>
/// Heavy strategies (registered via
/// <see cref="PromptProcessingIntentBuilder.WithHeavyStrategy{TStrategy}"/>) are only executed
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
    public async Task ProcessAsync(IntentProcessingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var intent = context.Result.Intent;
        ArgumentException.ThrowIfNullOrEmpty(intent);

        var enableHeavyStrategies = _options.EnableHeavyProcessingStrategies;

        foreach (var strategy in _strategies)
        {
            // Skip heavy strategies if not enabled
            if (_options.HeavyStrategyTypes.Contains(strategy.GetType()) && !enableHeavyStrategies)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        "Skipping heavy strategy {StrategyType} for intent {Intent} because EnableHeavyProcessingStrategies is false.",
                        strategy.GetType().Name, intent);
                }
                continue;
            }

            await ExecuteStrategyAsync(strategy, context, intent, cancellationToken);
        }

        if (!context.Result.HasContext)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("No strategy added context for intent {Intent}.", intent);
            }
        }
    }

    private async Task ExecuteStrategyAsync(
        IPromptProcessingStrategy strategy,
        IntentProcessingContext context,
        string intent,
        CancellationToken cancellationToken)
    {
        try
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Calling strategy {StrategyType} for intent {Intent}.", strategy.GetType().Name, intent);
            }

            var contextCountBefore = context.Result.AdditionalContexts.Count;

            await strategy.ProcessAsync(context, cancellationToken);

            var contextCountAfter = context.Result.AdditionalContexts.Count;

            if (_logger.IsEnabled(LogLevel.Debug))
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
}
