namespace CrestApps.OrchardCore.BusinessHours.Services;

/// <summary>
/// Hears that the hours of a Business Hours schedule are about to change, and that they changed, so a module that counts
/// time on the schedule (such as service level timers) can count the time so far on the old hours and the rest on the
/// new ones. Removing a schedule is a change too: from then on its id counts around the clock.
/// </summary>
public interface IBusinessHoursScheduleObserver
{
    /// <summary>
    /// Called before a schedule is changed or removed, while reads still return its old hours.
    /// </summary>
    /// <param name="scheduleId">The schedule.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task ChangingAsync(string scheduleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called after a schedule was changed or removed. Its new hours are readable once the change commits.
    /// </summary>
    /// <param name="scheduleId">The schedule.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task ChangedAsync(string scheduleId, CancellationToken cancellationToken = default);
}
