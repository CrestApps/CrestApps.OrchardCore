namespace CrestApps.Core.AI.Models;

/// <summary>
/// Stores the AI-evaluated result for a single conversion goal.
/// </summary>
public sealed class ConversionGoalResult
{
    /// <summary>
    /// Gets or sets the name of the goal that was evaluated.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the AI-assigned score for this goal.
    /// </summary>
    public int Score { get; set; }

    /// <summary>
    /// Gets or sets the maximum possible score for this goal.
    /// </summary>
    public int MaxScore { get; set; }

    /// <summary>
    /// Gets or sets an optional AI-generated explanation for the assigned score.
    /// </summary>
    public string Reasoning { get; set; }
}
