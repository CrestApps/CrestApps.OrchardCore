namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Identifies whether an omnichannel activity is available for assignment or currently owned by an agent, queue, or dialer.
/// </summary>
public enum ActivityAssignmentStatus
{
    /// <summary>
    /// The activity has no owner and can be considered by routing or dialer components.
    /// </summary>
    Unassigned,

    /// <summary>
    /// The activity is available for routing or dialing.
    /// </summary>
    Available,

    /// <summary>
    /// The activity is temporarily reserved by a routing or dialer component.
    /// </summary>
    Reserved,

    /// <summary>
    /// The activity is assigned to an agent or workflow owner.
    /// </summary>
    Assigned,

    /// <summary>
    /// The activity is actively being worked.
    /// </summary>
    InProgress,

    /// <summary>
    /// The activity assignment has been released.
    /// </summary>
    Released,
}
