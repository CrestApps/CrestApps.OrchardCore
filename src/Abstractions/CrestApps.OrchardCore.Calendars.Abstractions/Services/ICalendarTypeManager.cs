namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// Reads the calendar types and event types the enabled features register.
/// </summary>
public interface ICalendarTypeManager
{
    /// <summary>
    /// Lists the registered calendar types in the order they should be shown.
    /// </summary>
    /// <returns>The calendar types.</returns>
    IReadOnlyList<CalendarTypeDescriptor> ListTypes();

    /// <summary>
    /// Finds a calendar type by name.
    /// </summary>
    /// <param name="name">The type's technical name.</param>
    /// <returns>The type, or <see langword="null"/> when no enabled feature registers it.</returns>
    CalendarTypeDescriptor FindType(string name);

    /// <summary>
    /// Lists the event types calendars of a type hold. A type that names none holds the default event type.
    /// </summary>
    /// <param name="calendarType">The calendar type's technical name.</param>
    /// <returns>The event types.</returns>
    IReadOnlyList<CalendarEventTypeDescriptor> ListEventTypes(string calendarType);

    /// <summary>
    /// Finds an event type of a calendar type, falling back to the type's first event type when the name is empty.
    /// </summary>
    /// <param name="calendarType">The calendar type's technical name.</param>
    /// <param name="eventType">The event type's technical name.</param>
    /// <returns>The event type, or <see langword="null"/> when the calendar type has none by that name.</returns>
    CalendarEventTypeDescriptor FindEventType(string calendarType, string eventType);
}
