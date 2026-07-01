using CrestApps.OrchardCore.ContactCenter.Core.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IBusinessHoursService"/> backed by the calendar catalog.
/// </summary>
public sealed class DefaultBusinessHoursService : IBusinessHoursService
{
    private readonly IBusinessHoursCalendarManager _calendarManager;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultBusinessHoursService"/> class.
    /// </summary>
    /// <param name="calendarManager">The business-hours calendar manager.</param>
    /// <param name="clock">The clock used to resolve the current instant.</param>
    public DefaultBusinessHoursService(
        IBusinessHoursCalendarManager calendarManager,
        IClock clock)
    {
        _calendarManager = calendarManager;
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
        if (string.IsNullOrEmpty(calendarId))
        {
            return true;
        }

        var calendar = await _calendarManager.FindByIdAsync(calendarId, cancellationToken);

        if (calendar is null || !calendar.Enabled)
        {
            return true;
        }

        return IsOpen(calendar, utcInstant);
    }

    /// <summary>
    /// Evaluates whether the supplied calendar is open at the given UTC instant.
    /// </summary>
    /// <param name="calendar">The calendar to evaluate.</param>
    /// <param name="utcInstant">The UTC instant to evaluate.</param>
    /// <returns><see langword="true"/> when the calendar is open; otherwise, <see langword="false"/>.</returns>
    public static bool IsOpen(BusinessHoursCalendar calendar, DateTime utcInstant)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        var timeZone = ResolveTimeZone(calendar.TimeZoneId);
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcInstant, DateTimeKind.Utc), timeZone);
        var localDate = DateOnly.FromDateTime(local);

        if (calendar.Holidays is not null && calendar.Holidays.Contains(localDate))
        {
            return false;
        }

        var window = calendar.WeeklySchedule?.FirstOrDefault(day => day.Day == local.DayOfWeek);

        if (window is null || !window.IsOpen || window.CloseMinute <= window.OpenMinute)
        {
            return false;
        }

        var minuteOfDay = (local.Hour * 60) + local.Minute;

        return minuteOfDay >= window.OpenMinute && minuteOfDay < window.CloseMinute;
    }

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
