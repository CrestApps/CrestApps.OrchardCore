using CrestApps.OrchardCore.BusinessHours.Models;

namespace CrestApps.OrchardCore.BusinessHours.Services;

/// <summary>
/// Keeps hours schedules, such as the business hours calendars of the Contact Center. A feature that stores schedules
/// registers one with <see cref="BusinessHoursServiceCollectionExtensions.AddHoursScheduleSource{TSource}"/>, and
/// <see cref="IOpenHoursService"/> reads it.
/// </summary>
public interface IHoursScheduleSource
{
    /// <summary>
    /// Finds a schedule.
    /// </summary>
    /// <param name="scheduleId">The schedule identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The schedule, or <see langword="null"/> when this source doesn't know it or it is disabled.</returns>
    Task<HoursSchedule> FindAsync(string scheduleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the schedules this source offers for picking.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The schedules.</returns>
    Task<IReadOnlyList<HoursScheduleOption>> ListAsync(CancellationToken cancellationToken = default);
}
