namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// The state of a schedule period.
/// </summary>
public enum SchedulePeriodStatus
{
    /// <summary>
    /// Being planned; workers don't see it.
    /// </summary>
    Draft,

    /// <summary>
    /// Published to the team's calendar. It can still be changed and published again.
    /// </summary>
    Published,

    /// <summary>
    /// Closed; it can no longer be changed.
    /// </summary>
    Locked,
}
