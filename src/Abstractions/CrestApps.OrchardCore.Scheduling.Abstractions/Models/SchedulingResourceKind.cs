namespace CrestApps.OrchardCore.Scheduling.Models;

/// <summary>
/// What kind of resource is scheduled.
/// </summary>
public enum SchedulingResourceKind
{
    /// <summary>
    /// A person, who is a user of the site.
    /// </summary>
    Staff,

    /// <summary>
    /// A room or another space.
    /// </summary>
    Room,

    /// <summary>
    /// A piece of equipment.
    /// </summary>
    Equipment,

    /// <summary>
    /// Anything else that takes bookings.
    /// </summary>
    Other,
}
