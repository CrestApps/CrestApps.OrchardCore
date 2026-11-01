namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// What happens when a worker forgets to clock out.
/// </summary>
public enum MissedPunchHandling
{
    /// <summary>
    /// The open time is left out and a missed punch is raised for a manager to fix.
    /// </summary>
    Flag,

    /// <summary>
    /// The time is closed at the end of the worker's shift, and a missed punch is still raised.
    /// </summary>
    CloseAtScheduledEnd,
}
