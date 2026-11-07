using CrestApps.OrchardCore.BusinessHours;
using CrestApps.OrchardCore.BusinessHours.Services;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IBusinessHoursService"/> over the shared
/// <see cref="IOpenHoursService"/>, so a queue, entry point or dialer profile reads its calendar the way every other
/// module reads business hours.
/// </summary>
public sealed class DefaultBusinessHoursService : IBusinessHoursService
{
    private readonly IOpenHoursService _openHoursService;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultBusinessHoursService"/> class.
    /// </summary>
    /// <param name="openHoursService">The shared open-hours service.</param>
    /// <param name="clock">The clock used to resolve the current instant.</param>
    public DefaultBusinessHoursService(
        IOpenHoursService openHoursService,
        IClock clock)
    {
        _openHoursService = openHoursService;
        _clock = clock;
    }

    /// <inheritdoc/>
    public Task<bool> IsOpenAsync(string calendarId, CancellationToken cancellationToken = default)
    {
        return IsOpenAsync(calendarId, _clock.UtcNow, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> IsOpenAsync(string calendarId, DateTime utcInstant, CancellationToken cancellationToken = default)
    {
        return await IsOpenAsync(calendarId, utcInstant, null, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> IsOpenAsync(
        string calendarId,
        DateTime utcInstant,
        string timeZoneId,
        CancellationToken cancellationToken = default)
    {
        return await EvaluateAsync(calendarId, utcInstant, timeZoneId, cancellationToken) ?? true;
    }

    /// <inheritdoc/>
    public Task<bool?> EvaluateAsync(
        string calendarId,
        DateTime utcInstant,
        string timeZoneId,
        CancellationToken cancellationToken = default)
    {
        return _openHoursService.IsOpenAsync(calendarId, utcInstant, timeZoneId, cancellationToken);
    }

    /// <summary>
    /// Evaluates whether the supplied calendar is open at the given UTC instant.
    /// </summary>
    /// <param name="calendar">The calendar to evaluate.</param>
    /// <param name="utcInstant">The UTC instant to evaluate.</param>
    /// <returns><see langword="true"/> when the calendar is open; otherwise, <see langword="false"/>.</returns>
    public static bool IsOpen(BusinessHoursCalendar calendar, DateTime utcInstant)
    {
        return IsOpen(calendar, utcInstant, null);
    }

    /// <summary>
    /// Evaluates whether the supplied calendar is open at the given UTC instant using an optional time-zone override,
    /// with the shared <see cref="HoursScheduleEvaluator"/>.
    /// </summary>
    /// <param name="calendar">The calendar to evaluate.</param>
    /// <param name="utcInstant">The UTC instant to evaluate.</param>
    /// <param name="timeZoneId">The time zone used instead of the calendar time zone when supplied.</param>
    /// <returns><see langword="true"/> when the calendar is open; otherwise, <see langword="false"/>.</returns>
    public static bool IsOpen(BusinessHoursCalendar calendar, DateTime utcInstant, string timeZoneId)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        return HoursScheduleEvaluator.IsOpen(calendar.ToHoursSchedule(), utcInstant, timeZoneId);
    }

    /// <summary>
    /// Gives the stretches the supplied calendar is open between two UTC times, with the shared
    /// <see cref="HoursScheduleEvaluator"/>.
    /// </summary>
    /// <param name="calendar">The calendar to evaluate.</param>
    /// <param name="fromUtc">The start.</param>
    /// <param name="toUtc">The end.</param>
    /// <param name="timeZoneId">The time zone used instead of the calendar time zone when supplied.</param>
    /// <returns>The open stretches, in order, merged where they touch and clipped to the range.</returns>
    public static IReadOnlyList<TimeWindow> GetOpenWindows(BusinessHoursCalendar calendar, DateTime fromUtc, DateTime toUtc, string timeZoneId)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        return HoursScheduleEvaluator.GetOpenWindows(calendar.ToHoursSchedule(), fromUtc, toUtc, timeZoneId);
    }
}
