namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// Who a time-off policy applies to: a team (and its sub-teams) or one worker, for a range of dates.
/// </summary>
public sealed class TimeOffPolicyAssignment
{
    /// <summary>
    /// Gets or sets the team, for a team assignment.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the worker, for a worker assignment, which takes precedence over their teams'.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets the first day the assignment counts; empty for no start.
    /// </summary>
    public DateOnly? EffectiveFrom { get; set; }

    /// <summary>
    /// Gets or sets the last day the assignment counts; empty for no end.
    /// </summary>
    public DateOnly? EffectiveTo { get; set; }

    /// <summary>
    /// Returns whether the assignment counts on a day.
    /// </summary>
    /// <param name="date">The day.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    public bool IsEffectiveOn(DateOnly date)
    {
        return (!EffectiveFrom.HasValue || EffectiveFrom.Value <= date) && (!EffectiveTo.HasValue || date <= EffectiveTo.Value);
    }
}
