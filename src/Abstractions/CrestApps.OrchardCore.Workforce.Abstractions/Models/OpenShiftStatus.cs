namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// Where an open shift stands.
/// </summary>
public enum OpenShiftStatus
{
    /// <summary>
    /// Offered and nobody has claimed it yet.
    /// </summary>
    Open,

    /// <summary>
    /// Claims are waiting for a decision.
    /// </summary>
    Claimed,

    /// <summary>
    /// Given to a worker; the shift is on the calendar.
    /// </summary>
    Filled,

    /// <summary>
    /// No longer offered.
    /// </summary>
    Canceled,
}
