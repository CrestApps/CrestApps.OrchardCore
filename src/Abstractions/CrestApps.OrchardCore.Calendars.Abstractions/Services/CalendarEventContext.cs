using CrestApps.OrchardCore.Calendars.Models;

namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// An event being changed.
/// </summary>
public class CalendarEventContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarEventContext"/> class.
    /// </summary>
    /// <param name="calendarEvent">The event.</param>
    /// <param name="previous">A copy of the event as it was, or <see langword="null"/> for a new event.</param>
    /// <param name="calendar">The event's calendar.</param>
    /// <param name="calendarType">The calendar's type, or <see langword="null"/> when it is no longer registered.</param>
    /// <param name="eventType">The event's type, or <see langword="null"/> when it is no longer registered.</param>
    /// <param name="kind">What is happening to the event.</param>
    /// <param name="options">How the change is made.</param>
    public CalendarEventContext(
        CalendarEvent calendarEvent,
        CalendarEvent previous,
        Calendar calendar,
        CalendarTypeDescriptor calendarType,
        CalendarEventTypeDescriptor eventType,
        CalendarEventChangeKind kind,
        CalendarChangeOptions options)
    {
        ArgumentNullException.ThrowIfNull(calendarEvent);
        ArgumentNullException.ThrowIfNull(calendar);

        Event = calendarEvent;
        Previous = previous;
        Calendar = calendar;
        CalendarType = calendarType;
        EventType = eventType;
        Kind = kind;
        Options = options ?? new CalendarChangeOptions();
    }

    /// <summary>
    /// Gets the event.
    /// </summary>
    public CalendarEvent Event { get; }

    /// <summary>
    /// Gets a copy of the event as it was before the change, or <see langword="null"/> for a new event.
    /// </summary>
    public CalendarEvent Previous { get; }

    /// <summary>
    /// Gets the event's calendar.
    /// </summary>
    public Calendar Calendar { get; }

    /// <summary>
    /// Gets the calendar's type.
    /// </summary>
    public CalendarTypeDescriptor CalendarType { get; }

    /// <summary>
    /// Gets the event's type.
    /// </summary>
    public CalendarEventTypeDescriptor EventType { get; }

    /// <summary>
    /// Gets what is happening to the event.
    /// </summary>
    public CalendarEventChangeKind Kind { get; }

    /// <summary>
    /// Gets how the change is made.
    /// </summary>
    public CalendarChangeOptions Options { get; }
}
