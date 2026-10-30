namespace CrestApps.OrchardCore.Calendars.Models;

/// <summary>
/// What happened to an event.
/// </summary>
public enum CalendarEventChangeKind
{
    /// <summary>
    /// The event was created.
    /// </summary>
    Created,

    /// <summary>
    /// The event's details or time changed.
    /// </summary>
    Updated,

    /// <summary>
    /// The event was cancelled.
    /// </summary>
    Cancelled,

    /// <summary>
    /// The event was deleted.
    /// </summary>
    Deleted,

    /// <summary>
    /// An attendee was invited.
    /// </summary>
    AttendeeAdded,

    /// <summary>
    /// An attendee was removed.
    /// </summary>
    AttendeeRemoved,

    /// <summary>
    /// An attendee answered.
    /// </summary>
    ResponseChanged,

    /// <summary>
    /// One occurrence of a repeating event was changed or cancelled.
    /// </summary>
    OccurrenceChanged,
}
