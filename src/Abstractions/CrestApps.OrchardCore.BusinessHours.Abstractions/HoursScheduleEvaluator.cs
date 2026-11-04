using CrestApps.OrchardCore.BusinessHours.Models;
using CrestApps.OrchardCore.Calendars;

namespace CrestApps.OrchardCore.BusinessHours;

/// <summary>
/// Reads an <see cref="HoursSchedule"/> in its time zone: whether it is open at an instant, and its open windows over a
/// range. Pure, so any module can evaluate a schedule it holds without a service.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Each local date owns its day from midnight to midnight. A date exception closed all day is closed for the whole
/// date; one with special hours is open only in them.</item>
/// <item>A window that closes before it opens runs past midnight: the part after midnight belongs to the next date and is
/// open unless that date is an exception.</item>
/// <item>A window that opens and closes at the same minute is open all day.</item>
/// <item>Wall-clock times convert with <see cref="CalendarTime"/>: a time a daylight-saving change skips moves forward by
/// the gap, and one that happens twice is the earlier of the two, so nothing throws twice a year.</item>
/// </list>
/// </remarks>
public static class HoursScheduleEvaluator
{
    private const int MinutesPerDay = 1440;

    /// <summary>
    /// Gets whether a schedule is open at an instant.
    /// </summary>
    /// <param name="schedule">The schedule.</param>
    /// <param name="utcInstant">The instant, in UTC.</param>
    /// <param name="timeZoneId">A time zone to read the schedule in instead of its own; optional.</param>
    /// <returns><see langword="true"/> when it is open.</returns>
    public static bool IsOpen(HoursSchedule schedule, DateTime utcInstant, string timeZoneId = null)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        var zoneId = ZoneOf(schedule, timeZoneId);
        var local = CalendarTime.ToLocal(utcInstant, zoneId);
        var date = DateOnly.FromDateTime(local);
        var minute = (local.Hour * 60) + local.Minute;

        return GetDayStretches(schedule, date).Any(stretch => minute >= stretch.Open && minute < stretch.Close);
    }

    /// <summary>
    /// Gets the stretches a schedule is open between two instants.
    /// </summary>
    /// <param name="schedule">The schedule.</param>
    /// <param name="fromUtc">The start, in UTC.</param>
    /// <param name="toUtc">The end, in UTC.</param>
    /// <param name="timeZoneId">A time zone to read the schedule in instead of its own; optional.</param>
    /// <returns>The open windows, in order, merged where they touch and clipped to the range.</returns>
    public static IReadOnlyList<TimeWindow> GetOpenWindows(HoursSchedule schedule, DateTime fromUtc, DateTime toUtc, string timeZoneId = null)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        var range = new TimeWindow(DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc), DateTime.SpecifyKind(toUtc, DateTimeKind.Utc));

        if (range.IsEmpty)
        {
            return [];
        }

        var zone = CalendarTime.GetZone(ZoneOf(schedule, timeZoneId));
        var firstDate = DateOnly.FromDateTime(CalendarTime.ToLocal(range.StartUtc, zone.Id));
        var lastDate = DateOnly.FromDateTime(CalendarTime.ToLocal(range.EndUtc, zone.Id));
        var windows = new List<TimeWindow>();

        for (var date = firstDate; date <= lastDate; date = date.AddDays(1))
        {
            foreach (var (open, close) in GetDayStretches(schedule, date))
            {
                windows.Add(new TimeWindow(ToUtc(date, open, zone), ToUtc(date, close, zone)));
            }
        }

        return TimeWindow.Clip(TimeWindow.Merge(windows), range);
    }

    /// <summary>
    /// Gets the minutes of a local date the schedule is open, as [open, close) pairs within the day.
    /// </summary>
    /// <param name="schedule">The schedule.</param>
    /// <param name="date">The local date.</param>
    /// <returns>The open stretches of the date.</returns>
    public static IEnumerable<(int Open, int Close)> GetDayStretches(HoursSchedule schedule, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        var exception = FindException(schedule, date);

        if (exception is not null)
        {
            if (!exception.Closed && IsValid(exception.OpenMinute, exception.CloseMinute))
            {
                yield return SameDayPart(exception.OpenMinute, exception.CloseMinute);
            }

            yield break;
        }

        foreach (var window in WindowsOn(schedule, date.DayOfWeek))
        {
            yield return SameDayPart(window.OpenMinute, window.CloseMinute);
        }

        // The part after midnight of a window that opened the day before and runs past midnight.
        var previous = date.AddDays(-1);
        var previousException = FindException(schedule, previous);

        if (previousException is not null)
        {
            if (!previousException.Closed && IsOvernight(previousException.OpenMinute, previousException.CloseMinute))
            {
                yield return (0, previousException.CloseMinute);
            }

            // A weekly window that opened on a holiday still runs into the next date, which is not a holiday.
            if (!previousException.Closed)
            {
                yield break;
            }
        }

        foreach (var window in WindowsOn(schedule, previous.DayOfWeek).Where(window => IsOvernight(window.OpenMinute, window.CloseMinute)))
        {
            yield return (0, window.CloseMinute);
        }
    }

    private static string ZoneOf(HoursSchedule schedule, string timeZoneId)
        => string.IsNullOrWhiteSpace(timeZoneId) ? schedule.TimeZoneId : timeZoneId;

    private static HoursDateException FindException(HoursSchedule schedule, DateOnly date)
        => schedule.DateExceptions?.FirstOrDefault(exception => exception is not null && exception.Date == date);

    private static IEnumerable<HoursWindow> WindowsOn(HoursSchedule schedule, DayOfWeek day)
        => (schedule.Windows ?? []).Where(window => window is not null && window.Day == day && IsValid(window.OpenMinute, window.CloseMinute));

    private static (int Open, int Close) SameDayPart(int open, int close)
        => open == close ? (0, MinutesPerDay) : open < close ? (open, close) : (open, MinutesPerDay);

    private static bool IsOvernight(int open, int close)
        => IsValid(open, close) && open > close && close > 0;

    private static bool IsValid(int open, int close)
        => open is >= 0 and <= MinutesPerDay && close is >= 0 and <= MinutesPerDay;

    private static DateTime ToUtc(DateOnly date, int minuteOfDay, NodaTime.DateTimeZone zone)
        => CalendarTime.ToUtc(CalendarTime.ToLocalDateTime(date.ToDateTime(TimeOnly.MinValue).AddMinutes(minuteOfDay)), zone);
}
