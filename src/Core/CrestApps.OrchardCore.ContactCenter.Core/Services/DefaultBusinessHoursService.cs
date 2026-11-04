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
    public async Task<bool?> EvaluateAsync(
        string calendarId,
        DateTime utcInstant,
        string timeZoneId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(calendarId))
        {
            return null;
        }

        var calendar = await _calendarManager.FindByIdAsync(calendarId, cancellationToken);

        if (calendar is null || !calendar.Enabled)
        {
            return null;
        }

        return IsOpen(calendar, utcInstant, timeZoneId);
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
    /// Evaluates whether the supplied calendar is open at the given UTC instant using an optional time-zone override.
    /// </summary>
    /// <param name="calendar">The calendar to evaluate.</param>
    /// <param name="utcInstant">The UTC instant to evaluate.</param>
    /// <param name="timeZoneId">The time zone used instead of the calendar time zone when supplied.</param>
    /// <returns><see langword="true"/> when the calendar is open; otherwise, <see langword="false"/>.</returns>
    public static bool IsOpen(BusinessHoursCalendar calendar, DateTime utcInstant, string timeZoneId)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        var timeZone = ResolveTimeZone(string.IsNullOrWhiteSpace(timeZoneId) ? calendar.TimeZoneId : timeZoneId);
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcInstant, DateTimeKind.Utc), timeZone);
        var localDate = DateOnly.FromDateTime(local);

        if (calendar.Holidays is not null && calendar.Holidays.Contains(localDate))
        {
            return false;
        }

        var schedule = calendar.WeeklySchedule;
        var window = schedule?.FirstOrDefault(day => day.Day == local.DayOfWeek);
        var minuteOfDay = (local.Hour * 60) + local.Minute;

        if (IsWithinSameDayWindow(window, minuteOfDay))
        {
            return true;
        }

        var previousDay = local.DayOfWeek == DayOfWeek.Sunday
            ? DayOfWeek.Saturday
            : (DayOfWeek)((int)local.DayOfWeek - 1);
        var previousWindow = schedule?.FirstOrDefault(day => day.Day == previousDay);

        return IsWithinPreviousOvernightWindow(previousWindow, minuteOfDay);
    }

    /// <summary>
    /// Gives the stretches the supplied calendar is open between two UTC times, by the same rules as
    /// <see cref="IsOpen(BusinessHoursCalendar, DateTime, string)"/>: a day's window, a window that closes before it opens
    /// running past midnight, and holidays closed all day.
    /// </summary>
    /// <remarks>
    /// An opening or closing time that a daylight-saving change skips moves forward by the gap, and one that happens twice
    /// is the earlier of the two.
    /// </remarks>
    /// <param name="calendar">The calendar to evaluate.</param>
    /// <param name="fromUtc">The start.</param>
    /// <param name="toUtc">The end.</param>
    /// <param name="timeZoneId">The time zone used instead of the calendar time zone when supplied.</param>
    /// <returns>The open stretches, in order, merged where they touch and clipped to the range.</returns>
    public static IReadOnlyList<(DateTime StartUtc, DateTime EndUtc)> GetOpenWindows(BusinessHoursCalendar calendar, DateTime fromUtc, DateTime toUtc, string timeZoneId)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        var from = DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc);
        var to = DateTime.SpecifyKind(toUtc, DateTimeKind.Utc);

        if (to <= from)
        {
            return [];
        }

        var timeZone = ResolveTimeZone(string.IsNullOrWhiteSpace(timeZoneId) ? calendar.TimeZoneId : timeZoneId);
        var schedule = calendar.WeeklySchedule ?? [];
        var stretches = new List<(DateTime StartUtc, DateTime EndUtc)>();
        var lastDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(to, timeZone));

        for (var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(from, timeZone)).AddDays(-1); day <= lastDay; day = day.AddDays(1))
        {
            if (calendar.Holidays is not null && calendar.Holidays.Contains(day))
            {
                continue;
            }

            var window = schedule.FirstOrDefault(item => item.Day == day.DayOfWeek);

            if (IsValidWindow(window))
            {
                var (open, close) = window.OpenMinute == window.CloseMinute
                    ? (0, 1440)
                    : window.OpenMinute < window.CloseMinute
                        ? (window.OpenMinute, window.CloseMinute)
                        : (window.OpenMinute, 1440);

                stretches.Add((ToUtc(day, open, timeZone), ToUtc(day, close, timeZone)));
            }

            var previous = schedule.FirstOrDefault(item => item.Day == day.AddDays(-1).DayOfWeek);

            if (IsValidWindow(previous) && previous.OpenMinute > previous.CloseMinute && previous.CloseMinute > 0)
            {
                stretches.Add((ToUtc(day, 0, timeZone), ToUtc(day, previous.CloseMinute, timeZone)));
            }
        }

        var merged = new List<(DateTime StartUtc, DateTime EndUtc)>();

        foreach (var (start, end) in stretches.OrderBy(stretch => stretch.StartUtc))
        {
            var clippedStart = start < from ? from : start;
            var clippedEnd = end > to ? to : end;

            if (clippedEnd <= clippedStart)
            {
                continue;
            }

            if (merged.Count > 0 && merged[^1].EndUtc >= clippedStart)
            {
                merged[^1] = (merged[^1].StartUtc, clippedEnd > merged[^1].EndUtc ? clippedEnd : merged[^1].EndUtc);

                continue;
            }

            merged.Add((clippedStart, clippedEnd));
        }

        return merged;
    }

    // Converts a wall-clock minute of a day to UTC without throwing in a daylight-saving gap: a skipped time moves forward
    // by the gap, and a time that happens twice is the earlier of the two.
    private static DateTime ToUtc(DateOnly day, int minuteOfDay, TimeZoneInfo timeZone)
    {
        var local = day.ToDateTime(TimeOnly.MinValue).AddMinutes(minuteOfDay);
        TimeSpan offset;

        if (timeZone.IsInvalidTime(local))
        {
            offset = timeZone.GetUtcOffset(local.AddHours(-3));
        }
        else if (timeZone.IsAmbiguousTime(local))
        {
            offset = timeZone.GetAmbiguousTimeOffsets(local).Max();
        }
        else
        {
            offset = timeZone.GetUtcOffset(local);
        }

        return DateTime.SpecifyKind(local - offset, DateTimeKind.Utc);
    }

    private static bool IsWithinSameDayWindow(BusinessHoursDay window, int minuteOfDay)
    {
        if (!IsValidWindow(window))
        {
            return false;
        }

        if (window.OpenMinute == window.CloseMinute)
        {
            return true;
        }

        if (window.OpenMinute < window.CloseMinute)
        {
            return minuteOfDay >= window.OpenMinute && minuteOfDay < window.CloseMinute;
        }

        return minuteOfDay >= window.OpenMinute;
    }

    private static bool IsWithinPreviousOvernightWindow(BusinessHoursDay window, int minuteOfDay)
    {
        return IsValidWindow(window) &&
            window.OpenMinute > window.CloseMinute &&
            minuteOfDay < window.CloseMinute;
    }

    private static bool IsValidWindow(BusinessHoursDay window)
    {
        return window is not null &&
            window.IsOpen &&
            window.OpenMinute is >= 0 and <= 1440 &&
            window.CloseMinute is >= 0 and <= 1440;
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
