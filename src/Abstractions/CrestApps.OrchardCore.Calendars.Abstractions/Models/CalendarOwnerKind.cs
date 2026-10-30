namespace CrestApps.OrchardCore.Calendars.Models;

/// <summary>
/// Who a calendar belongs to.
/// </summary>
public enum CalendarOwnerKind
{
    /// <summary>
    /// Nobody in particular, such as a shared company calendar.
    /// </summary>
    None,

    /// <summary>
    /// One user, whose user id is the owner id.
    /// </summary>
    User,

    /// <summary>
    /// A team, whose role name is the owner id.
    /// </summary>
    Role,

    /// <summary>
    /// A resource such as a room or a piece of equipment; the calendar itself is the resource.
    /// </summary>
    Resource,
}
