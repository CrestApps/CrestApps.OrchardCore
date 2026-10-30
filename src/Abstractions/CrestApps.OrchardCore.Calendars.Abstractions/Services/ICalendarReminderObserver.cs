namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// Hears about occurrences that are about to start or have ended. The Calendar Reminders feature raises each moment exactly
/// once per occurrence and offset, even across restarts and servers.
/// </summary>
public interface ICalendarReminderObserver
{
    /// <summary>
    /// Handles an occurrence that starts within the reminder's offset.
    /// </summary>
    /// <param name="context">The occurrence and the offset.</param>
    /// <returns>A task.</returns>
    Task OnStartingSoonAsync(CalendarReminderContext context);

    /// <summary>
    /// Handles an occurrence that has ended.
    /// </summary>
    /// <param name="context">The occurrence.</param>
    /// <returns>A task.</returns>
    Task OnCompletedAsync(CalendarReminderContext context);
}
