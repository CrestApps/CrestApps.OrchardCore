namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// Takes part in calendar changes: validates them and reacts to them within the request. Derive from
/// <see cref="CalendarHandlerBase"/> to implement only what you need.
/// </summary>
public interface ICalendarHandler
{
    /// <summary>
    /// Validates a calendar before it is saved. Add errors to refuse the save.
    /// </summary>
    /// <param name="context">The calendar and the errors.</param>
    /// <returns>A task.</returns>
    Task ValidatingAsync(CalendarValidatingContext context);

    /// <summary>
    /// Runs after a new calendar is staged for saving.
    /// </summary>
    /// <param name="context">The calendar.</param>
    /// <returns>A task.</returns>
    Task CreatedAsync(CalendarContext context);

    /// <summary>
    /// Runs after a changed calendar is staged for saving.
    /// </summary>
    /// <param name="context">The calendar.</param>
    /// <returns>A task.</returns>
    Task UpdatedAsync(CalendarContext context);

    /// <summary>
    /// Runs before a calendar and its events are deleted.
    /// </summary>
    /// <param name="context">The calendar.</param>
    /// <returns>A task.</returns>
    Task DeletingAsync(CalendarContext context);
}
