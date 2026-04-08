namespace CrestApps.Core.AI.Models;

/// <summary>
/// Defines a single conversion goal used to measure chat session success.
/// Each goal is evaluated by AI after session close and scored within the configured range.
/// </summary>
public sealed class ConversionGoal
{
    /// <summary>
    /// Gets or sets the unique name for this goal.
    /// Must be alphanumeric with underscores only.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets a human-readable description of what constitutes success for this goal.
    /// This is provided to the AI model as evaluation criteria.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the minimum score for this goal. Defaults to 0.
    /// </summary>
    public int MinScore { get; set; }

    /// <summary>
    /// Gets or sets the maximum score for this goal. Defaults to 10.
    /// </summary>
    public int MaxScore { get; set; } = 10;
}
