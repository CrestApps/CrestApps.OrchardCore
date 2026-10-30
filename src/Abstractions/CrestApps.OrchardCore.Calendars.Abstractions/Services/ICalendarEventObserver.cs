namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// Hears about event changes after they are committed, in a new scope. Workflows, invitations, reminders and external
/// synchronization listen here, so they never act on a change that was rolled back.
/// </summary>
public interface ICalendarEventObserver
{
    /// <summary>
    /// Handles a committed change.
    /// </summary>
    /// <param name="change">What happened.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task OnChangedAsync(CalendarEventChange change, CancellationToken cancellationToken = default);
}
