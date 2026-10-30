namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// Takes part in event changes within the request: validates them (for example a calendar type's own rules) and reacts to them
/// before the session commits. For work that must only happen once the change is committed, such as running workflows or calling
/// another system, implement <see cref="ICalendarEventObserver"/> instead. Derive from <see cref="CalendarEventHandlerBase"/> to
/// implement only what you need.
/// </summary>
public interface ICalendarEventHandler
{
    /// <summary>
    /// Validates an event before it is saved. Add errors to refuse the save.
    /// </summary>
    /// <param name="context">The event, its calendar and type, the event as it was, and the errors.</param>
    /// <returns>A task.</returns>
    Task ValidatingAsync(CalendarEventValidatingContext context);

    /// <summary>
    /// Runs after an event was validated, just before it is staged for saving. The event may still be adjusted.
    /// </summary>
    /// <param name="context">The event.</param>
    /// <returns>A task.</returns>
    Task SavingAsync(CalendarEventContext context);

    /// <summary>
    /// Runs after an event is staged for saving.
    /// </summary>
    /// <param name="context">The event and what happened to it.</param>
    /// <returns>A task.</returns>
    Task SavedAsync(CalendarEventContext context);

    /// <summary>
    /// Runs before an event is deleted.
    /// </summary>
    /// <param name="context">The event.</param>
    /// <returns>A task.</returns>
    Task DeletingAsync(CalendarEventContext context);
}
