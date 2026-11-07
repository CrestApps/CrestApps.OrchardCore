using CrestApps.OrchardCore.BusinessHours.Models;
using CrestApps.OrchardCore.Calendars;

namespace CrestApps.OrchardCore.BusinessHours.Services;

/// <summary>
/// Answers "are we open?" for every module: queues, entry points and the dialer, automated sends, service level clocks.
/// It reads schedules from the registered <see cref="IHoursScheduleSource"/>s, so a consumer never knows which module
/// keeps them.
/// </summary>
public interface IOpenHoursService
{
    /// <summary>
    /// Gets whether a schedule is open at an instant.
    /// </summary>
    /// <param name="scheduleId">The schedule.</param>
    /// <param name="utcInstant">The instant, in UTC.</param>
    /// <param name="timeZoneId">A time zone to read the schedule in instead of its own, such as the contact's; optional.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// Whether it is open, or <see langword="null"/> when the id is empty or no source knows the schedule (or it is
    /// disabled); callers usually treat that as open, so a missing schedule never blocks live work.
    /// </returns>
    Task<bool?> IsOpenAsync(string scheduleId, DateTime utcInstant, string timeZoneId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the stretches a schedule is open between two instants.
    /// </summary>
    /// <param name="scheduleId">The schedule.</param>
    /// <param name="fromUtc">The start, in UTC.</param>
    /// <param name="toUtc">The end, in UTC.</param>
    /// <param name="timeZoneId">A time zone to read the schedule in instead of its own; optional.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// The open windows, in order, merged where they touch and clipped to the range; or <see langword="null"/> when no
    /// source knows the schedule.
    /// </returns>
    Task<IReadOnlyList<TimeWindow>> GetOpenWindowsAsync(string scheduleId, DateTime fromUtc, DateTime toUtc, string timeZoneId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the schedules every source offers, each once, ordered by name, for editor pickers.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The schedules.</returns>
    Task<IReadOnlyList<HoursScheduleOption>> ListSchedulesAsync(CancellationToken cancellationToken = default);
}
