using System.Globalization;
using CrestApps.OrchardCore.BusinessHours.Core.Models;
using CrestApps.OrchardCore.BusinessHours.Models;

namespace CrestApps.OrchardCore.BusinessHours.Core.Services;

/// <summary>
/// Turns the open windows of a week into the rows the weekly hours editor shows, one per day from Monday, and back.
/// Pure.
/// </summary>
public static class WeeklyHoursRows
{
    private const int MinutesPerDay = 1440;
    private const int DefaultOpenMinute = 9 * 60;
    private const int DefaultCloseMinute = 17 * 60;

    private static readonly DayOfWeek[] _week =
    [
        DayOfWeek.Monday,
        DayOfWeek.Tuesday,
        DayOfWeek.Wednesday,
        DayOfWeek.Thursday,
        DayOfWeek.Friday,
        DayOfWeek.Saturday,
        DayOfWeek.Sunday,
    ];

    /// <summary>
    /// Builds the rows of a week. With no windows at all, the rows offer Monday to Friday from 09:00 to 17:00, so a new
    /// schedule starts from common office hours. A day with more than two windows shows its first two.
    /// </summary>
    /// <param name="windows">The weekly windows.</param>
    /// <returns>Seven rows, from Monday.</returns>
    public static IList<WeeklyHoursRow> FromWindows(IEnumerable<HoursWindow> windows)
    {
        var all = (windows ?? []).Where(window => window is not null).ToList();
        var isNew = all.Count == 0;
        var rows = new List<WeeklyHoursRow>();

        foreach (var day in _week)
        {
            var dayWindows = all.Where(window => window.Day == day).OrderBy(window => window.OpenMinute).ToList();
            var first = dayWindows.FirstOrDefault();
            var second = dayWindows.Skip(1).FirstOrDefault();

            rows.Add(new WeeklyHoursRow
            {
                Day = day,
                DayName = CultureInfo.CurrentCulture.DateTimeFormat.GetDayName(day),
                Open = first is not null || (isNew && day is not DayOfWeek.Saturday and not DayOfWeek.Sunday),
                From = FormatMinute(first?.OpenMinute ?? DefaultOpenMinute),
                To = FormatMinute(first?.CloseMinute ?? DefaultCloseMinute),
                SecondFrom = second is null ? null : FormatMinute(second.OpenMinute),
                SecondTo = second is null ? null : FormatMinute(second.CloseMinute),
            });
        }

        return rows;
    }

    /// <summary>
    /// Builds the weekly windows from the editor's rows. A closed day has no window; a second window is kept only when
    /// both of its times are given. A closing time equal to the opening time is open all day, and an earlier one runs
    /// past midnight.
    /// </summary>
    /// <param name="rows">The rows.</param>
    /// <param name="invalidDays">Receives each open day whose times could not be read.</param>
    /// <returns>The windows.</returns>
    public static IList<HoursWindow> ToWindows(IEnumerable<WeeklyHoursRow> rows, ICollection<DayOfWeek> invalidDays = null)
    {
        var windows = new List<HoursWindow>();

        foreach (var row in rows ?? [])
        {
            if (row is null || !row.Open)
            {
                continue;
            }

            if (!TryParseMinute(row.From, out var open) || !TryParseMinute(row.To, out var close))
            {
                invalidDays?.Add(row.Day);

                continue;
            }

            windows.Add(new HoursWindow { Day = row.Day, OpenMinute = open, CloseMinute = close });

            if (string.IsNullOrWhiteSpace(row.SecondFrom) && string.IsNullOrWhiteSpace(row.SecondTo))
            {
                continue;
            }

            if (TryParseMinute(row.SecondFrom, out var secondOpen) && TryParseMinute(row.SecondTo, out var secondClose))
            {
                windows.Add(new HoursWindow { Day = row.Day, OpenMinute = secondOpen, CloseMinute = secondClose });
            }
            else
            {
                invalidDays?.Add(row.Day);
            }
        }

        return windows;
    }

    /// <summary>
    /// Formats minutes from midnight as <c>HH:mm</c>. Midnight at the end of a day (1440) is written as 00:00.
    /// </summary>
    /// <param name="minute">The minute.</param>
    /// <returns>The time.</returns>
    public static string FormatMinute(int minute)
    {
        var value = ((minute % MinutesPerDay) + MinutesPerDay) % MinutesPerDay;

        return string.Create(CultureInfo.InvariantCulture, $"{value / 60:D2}:{value % 60:D2}");
    }

    /// <summary>
    /// Reads a local time of day, such as <c>09:30</c>, as minutes from midnight.
    /// </summary>
    /// <param name="value">The time.</param>
    /// <param name="minute">The minutes from midnight.</param>
    /// <returns>Whether the value is a time.</returns>
    public static bool TryParseMinute(string value, out int minute)
    {
        if (!string.IsNullOrWhiteSpace(value)
            && TimeOnly.TryParse(value.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            minute = (time.Hour * 60) + time.Minute;

            return true;
        }

        minute = 0;

        return false;
    }
}
