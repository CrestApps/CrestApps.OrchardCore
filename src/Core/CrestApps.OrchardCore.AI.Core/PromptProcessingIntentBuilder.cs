using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.AI.Core;

/// <summary>
/// A fluent builder for configuring a registered prompt processing intent.
/// Returned by <see cref="ServiceCollectionExtensions.AddPromptProcessingIntent"/>.
/// </summary>
public sealed class PromptProcessingIntentBuilder
{
    private readonly IServiceCollection _services;
    private readonly string _intentName;

    internal PromptProcessingIntentBuilder(IServiceCollection services, string intentName)
    {
        _services = services;
        _intentName = intentName;
    }

    /// <summary>
    /// Marks this intent as heavy. Heavy intents are excluded from AI intent detection
    /// and their strategies are not invoked when
    /// <see cref="PromptProcessingOptions.EnableHeavyProcessingStrategies"/> is <c>false</c>.
    /// </summary>
    public PromptProcessingIntentBuilder AsHeavy()
    {
        _services.Configure<PromptProcessingOptions>(options =>
        {
            options.HeavyIntents.Add(_intentName);
        });

        return this;
    }

    /// <summary>
    /// Registers a first-phase strategy for this intent.
    /// The strategy is called during the first phase of prompt processing.
    /// </summary>
    /// <typeparam name="TStrategy">The strategy type implementing <see cref="IPromptProcessingStrategy"/>.</typeparam>
    public PromptProcessingIntentBuilder WithStrategy<TStrategy>()
        where TStrategy : class, IPromptProcessingStrategy
    {
        _services.TryAddEnumerable(ServiceDescriptor.Scoped<IPromptProcessingStrategy, TStrategy>());

        return this;
    }

    /// <summary>
    /// Registers a second-phase strategy for this intent and marks the intent as requiring
    /// second-phase processing. When this intent is detected, all registered second-phase
    /// strategies will be executed after the first-phase strategies complete.
    /// </summary>
    /// <typeparam name="TStrategy">The strategy type implementing <see cref="IPromptProcessingStrategy"/>.</typeparam>
    public PromptProcessingIntentBuilder WithSecondPhaseStrategy<TStrategy>()
        where TStrategy : class, IPromptProcessingStrategy
    {
        _services.Configure<PromptProcessingOptions>(options =>
        {
            options.SecondPhaseIntents.Add(_intentName);
            options.SecondPhaseStrategyTypes.Add(typeof(TStrategy));
        });

        _services.TryAddScoped<TStrategy>();

        return this;
    }
}
