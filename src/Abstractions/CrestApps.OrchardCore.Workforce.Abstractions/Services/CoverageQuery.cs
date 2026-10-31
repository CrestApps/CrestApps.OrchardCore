namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Which coverage to work out.
/// </summary>
public sealed class CoverageQuery
{
    /// <summary>
    /// Gets or sets the team.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the first day.
    /// </summary>
    public DateOnly From { get; set; }

    /// <summary>
    /// Gets or sets the last day.
    /// </summary>
    public DateOnly To { get; set; }

    /// <summary>
    /// Gets or sets the length of each interval.
    /// </summary>
    public int IntervalMinutes { get; set; } = 30;

    /// <summary>
    /// Gets or sets the draft scenario to count; <see langword="null"/> counts the published shifts.
    /// </summary>
    public string ScenarioId { get; set; }

    /// <summary>
    /// Gets or sets a skill to limit the requirements to, counting only the workers who hold it on the day;
    /// <see langword="null"/> for every requirement of the team, counting everyone.
    /// </summary>
    public string SkillId { get; set; }
}
