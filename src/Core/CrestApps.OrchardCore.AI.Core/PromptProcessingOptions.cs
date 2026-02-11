namespace CrestApps.OrchardCore.AI.Core;

/// <summary>
/// Options for configuring document processing intents and their descriptions.
/// Use this to register custom intents that will be recognized by the AI intent detector.
/// </summary>
public sealed class PromptProcessingOptions
{
    public const string SectionName = "CrestApps_AI:Chat";

    /// <summary>
    /// When false, heavy intents are excluded from AI intent detection and heavy strategies are not invoked.
    /// Default is false.
    /// </summary>
    public bool EnableHeavyProcessingStrategies { get; set; }

    /// <summary>
    /// Gets the dictionary of registered intents and their descriptions.
    /// The key is the intent name (e.g., "DocumentQnA") and the value is the description
    /// used by the AI intent detector to classify user prompts.
    /// </summary>
    internal Dictionary<string, string> InternalIntents { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the set of intent names that are considered heavy.
    /// These intents are filtered out from AI intent detection when <see cref="EnableHeavyProcessingStrategies"/> is false.
    /// </summary>
    internal HashSet<string> HeavyIntents { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the set of intent names that require second-phase processing.
    /// When an intent in this set is detected, second-phase strategies will be executed
    /// after the first-phase strategies complete.
    /// </summary>
    internal HashSet<string> SecondPhaseIntents { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the set of strategy types that should run during the second phase.
    /// These are registered via
    /// <see cref="PromptProcessingIntentBuilder.WithSecondPhaseStrategy{TStrategy}"/>.
    /// </summary>
    internal HashSet<Type> SecondPhaseStrategyTypes { get; } = [];

    public IReadOnlyDictionary<string, string> Intents => InternalIntents;
}
