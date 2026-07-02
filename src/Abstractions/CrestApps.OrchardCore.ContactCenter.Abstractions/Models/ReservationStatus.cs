namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Identifies the lifecycle state of an agent/activity reservation.
/// </summary>
public enum ReservationStatus
{
    /// <summary>
    /// The reservation is active and awaiting agent acceptance.
    /// </summary>
    Pending,

    /// <summary>
    /// The reservation was accepted and converted into an assignment.
    /// </summary>
    Accepted,

    /// <summary>
    /// The reservation was rejected by the agent.
    /// </summary>
    Rejected,

    /// <summary>
    /// The reservation expired before it was accepted.
    /// </summary>
    Expired,

    /// <summary>
    /// The reservation was canceled by the system or a supervisor.
    /// </summary>
    Canceled,
}
