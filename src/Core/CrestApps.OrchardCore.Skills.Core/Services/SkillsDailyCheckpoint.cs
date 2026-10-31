namespace CrestApps.OrchardCore.Skills.Core.Services;

/// <summary>
/// The day the daily skills check last ran, so it runs once a day however often its task wakes.
/// </summary>
public sealed class SkillsDailyCheckpoint
{
    /// <summary>
    /// Gets or sets the YesSql document id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the last day the check ran.
    /// </summary>
    public DateOnly? LastRun { get; set; }
}
