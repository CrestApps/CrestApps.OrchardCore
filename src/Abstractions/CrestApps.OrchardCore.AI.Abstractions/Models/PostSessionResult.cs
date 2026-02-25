namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Stores the result of a single post-session processing task.
/// </summary>
public sealed class PostSessionResult
{
    /// <summary>
    /// Gets or sets the name of the task that produced this result.
    /// Matches the <see cref="PostSessionTask.Name"/> from the profile configuration.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the AI-generated result value.
    /// For Disposition: the selected option. For Summary: the generated text. For Sentiment: Positive/Negative/Neutral.
    /// </summary>
    public string Value { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when this result was processed.
    /// </summary>
    public DateTime ProcessedAtUtc { get; set; }
}
