namespace CrestApps.OrchardCore.Calendars.Models;

/// <summary>
/// The part an attendee plays in an event.
/// </summary>
public enum CalendarAttendeeRole
{
    /// <summary>
    /// The attendee is expected to attend.
    /// </summary>
    Required,

    /// <summary>
    /// The attendee may attend.
    /// </summary>
    Optional,

    /// <summary>
    /// The attendee organizes the event.
    /// </summary>
    Organizer,

    /// <summary>
    /// A room or a piece of equipment the event uses.
    /// </summary>
    Resource,
}
