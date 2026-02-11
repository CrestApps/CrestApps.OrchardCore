using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Default implementation of <see cref="IPromptProcessingStrategyProvider"/> that routes
/// document processing through all registered strategies, allowing multiple to contribute context.
/// </summary>
/// <remarks>
/// <para>Heavy strategies (implementing <see cref="IHeavyPromptProcessingStrategy"/>) are only executed
/// when <see cref="PromptProcessingOptions.EnableHeavyProcessingStrategies"/> is true.</para>
///
/// <para>After all first-phase strategies run, if the detected intent is in
/// <see cref="PromptProcessingOptions.SecondPhaseIntents"/> or any first-phase strategy set
/// <see cref="IntentProcessingResult.RequiresSecondPhase"/>, all strategies registered via
/// <see cref="PromptProcessingIntentBuilder.WithSecondPhaseStrategy{TStrategy}"/> are resolved
/// and executed.</para>
/// </remarks>
public sealed class DefaultPromptProcessingStrategyProvider : IPromptProcessingStrategyProvider
{
    private readonly IEnumerable<IPromptProcessingStrategy> _strategies;
    private readonly IServiceProvider _serviceProvider;
    private readonly PromptProcessingOptions _options;
    private readonly ILogger _logger;

    public DefaultPromptProcessingStrategyProvider(
        IEnumerable<IPromptProcessingStrategy> strategies,
        IServiceProvider serviceProvider,
        IOptions<PromptProcessingOptions> options,
        ILogger<DefaultPromptProcessingStrategyProvider> logger)
    {
        _strategies = strategies;
        _serviceProvider = serviceProvider;
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

        // Auto-set RequiresSecondPhase if the detected intent is registered as a second-phase intent.
        if (_options.SecondPhaseIntents.Contains(intent))
        {
            context.Result.RequiresSecondPhase = true;
        }

        // First phase: call all strategies, allowing each to contribute context.
        foreach (var strategy in _strategies)
        {
            // Skip heavy strategies if not enabled
            if (strategy is IHeavyPromptProcessingStrategy && !enableHeavyStrategies)
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

        // Second phase: resolve and run additional strategies if the intent or any first-phase strategy requires it.
        if (context.Result.RequiresSecondPhase && _options.SecondPhaseStrategyTypes.Count > 0)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Running second-phase strategies for intent {Intent}.", intent);
            }

            foreach (var strategyType in _options.SecondPhaseStrategyTypes)
            {
                var strategy = (IPromptProcessingStrategy)_serviceProvider.GetRequiredService(strategyType);

                await ExecuteStrategyAsync(strategy, context, intent, cancellationToken);
            }
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
