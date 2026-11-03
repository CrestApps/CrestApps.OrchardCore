using CrestApps.OrchardCore.Calendars.Models;

namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// An event being validated.
/// </summary>
public sealed class CalendarEventValidatingContext : CalendarEventContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarEventValidatingContext"/> class.
    /// </summary>
    /// <param name="calendarEvent">The event.</param>
    /// <param name="previous">A copy of the event as it was, or <see langword="null"/> for a new event.</param>
    /// <param name="calendar">The event's calendar.</param>
    /// <param name="calendarType">The calendar's type.</param>
    /// <param name="eventType">The event's type.</param>
    /// <param name="kind">What is happening to the event.</param>
    /// <param name="options">How the change is made.</param>
    public CalendarEventValidatingContext(
        CalendarEvent calendarEvent,
        CalendarEvent previous,
        Calendar calendar,
        CalendarTypeDescriptor calendarType,
        CalendarEventTypeDescriptor eventType,
        CalendarEventChangeKind kind,
        CalendarChangeOptions options)
        : base(calendarEvent, previous, calendar, calendarType, eventType, kind, options)
    {
    }

    /// <summary>
    /// Gets the errors found so far. Add one to refuse the save.
    /// </summary>
    public IList<CalendarError> Errors { get; } = [];
}
