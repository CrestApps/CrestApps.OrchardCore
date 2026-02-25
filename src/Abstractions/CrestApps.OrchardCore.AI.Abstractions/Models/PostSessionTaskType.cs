namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Defines the type of post-session processing task.
/// </summary>
public enum PostSessionTaskType
{
    /// <summary>
    /// The AI selects one or more values from a predefined list of options.
    /// Used for dispositions, classifications, or any scenario where the user
    /// defines the possible outcomes upfront.
    /// </summary>
    PredefinedOptions,

    /// <summary>
    /// The AI generates a freeform text value based on the provided instructions.
    /// Used for summaries, sentiment analysis, or any open-ended analysis.
    /// </summary>
    Semantic,
}
