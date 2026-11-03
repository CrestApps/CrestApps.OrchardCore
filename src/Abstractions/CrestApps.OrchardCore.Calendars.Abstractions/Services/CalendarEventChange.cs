using CrestApps.OrchardCore.Calendars.Models;

namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// A committed change to an event, as told to the <see cref="ICalendarEventObserver"/>s.
/// </summary>
public sealed class CalendarEventChange
{
    /// <summary>
    /// Gets or sets what happened.
    /// </summary>
    public CalendarEventChangeKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the event as it is now.
    /// </summary>
    public CalendarEvent Event { get; set; }

    /// <summary>
    /// Gets or sets the event as it was before the change, or <see langword="null"/> for a new event.
    /// </summary>
    public CalendarEvent Previous { get; set; }

    /// <summary>
    /// Gets or sets the event's calendar.
    /// </summary>
    public Calendar Calendar { get; set; }

    /// <summary>
    /// Gets or sets when the changed occurrence was due to start, for a change to one occurrence of a repeating event.
    /// </summary>
    public DateTime? OccurrenceStartUtc { get; set; }

    /// <summary>
    /// Gets or sets the attendee the change is about, for attendee changes.
    /// </summary>
    public CalendarAttendee Attendee { get; set; }

    /// <summary>
    /// Gets or sets who made the change: a user id, <c>workflow</c>, or a module's name.
    /// </summary>
    public string Actor { get; set; }

    /// <summary>
    /// Gets or sets why the change was made.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets whether attendees should be emailed about the change.
    /// </summary>
    public bool SendInvitations { get; set; }
}
