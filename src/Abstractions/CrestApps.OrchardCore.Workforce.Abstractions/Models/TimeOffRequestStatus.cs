namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// Where a time-off request stands.
/// </summary>
public enum TimeOffRequestStatus
{
    /// <summary>
    /// Waiting for a manager.
    /// </summary>
    Pending,

    /// <summary>
    /// Waiting for a place, because as many people as may be off already are.
    /// </summary>
    Waitlisted,

    /// <summary>
    /// Approved; it is on the team calendar and taken from the balance.
    /// </summary>
    Approved,

    /// <summary>
    /// Turned down by a manager.
    /// </summary>
    Declined,

    /// <summary>
    /// Approved, then canceled; the balance was given back.
    /// </summary>
    Canceled,

    /// <summary>
    /// Taken back before a decision.
    /// </summary>
    Withdrawn,
}
