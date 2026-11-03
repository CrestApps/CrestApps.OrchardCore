namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// Tells the Calendar Reminders feature how long before an event starts its "starting soon" moment should be raised. Each
/// provider can return several offsets; each offset is raised once per occurrence.
/// </summary>
public interface ICalendarReminderOffsetProvider
{
    /// <summary>
    /// Gets how long before an occurrence starts to raise its "starting soon" moment.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The offsets.</returns>
    Task<IEnumerable<TimeSpan>> GetOffsetsAsync(CancellationToken cancellationToken = default);
}
