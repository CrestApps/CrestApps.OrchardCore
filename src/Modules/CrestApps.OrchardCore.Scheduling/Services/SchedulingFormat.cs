using System.Globalization;
using System.Text;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Scheduling.Core.Services;
using CrestApps.OrchardCore.Scheduling.Models;

namespace CrestApps.OrchardCore.Scheduling.Services;

/// <summary>
/// Reads and writes working hours as text, and lists open times for a day.
/// </summary>
public static class SchedulingFormat
{
    private static readonly DayOfWeek[] _week = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];

    /// <summary>
    /// Writes working hours as one line per day, such as <c>Monday 09:00-12:00, 13:00-17:00</c>.
    /// </summary>
    /// <param name="hours">The hours.</param>
    /// <returns>The text.</returns>
    public static string WriteHours(IEnumerable<SchedulingWorkingHours> hours)
    {
        var builder = new StringBuilder();

        foreach (var day in _week)
        {
            var windows = hours.Where(item => item.Day == day).Select(item => $"{item.Start}-{item.End}").ToList();

            if (windows.Count > 0)
            {
                builder.Append(day).Append(' ').AppendJoin(", ", windows).AppendLine();
            }
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// Reads working hours written by <see cref="WriteHours"/>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="errors">Lines that could not be read.</param>
    /// <returns>The hours.</returns>
    public static List<SchedulingWorkingHours> ReadHours(string text, IList<string> errors)
    {
        var result = new List<SchedulingWorkingHours>();

        foreach (var line in Lines(text))
        {
            var space = line.IndexOf(' ');

            if (space <= 0 || !Enum.TryParse<DayOfWeek>(line.Substring(0, space), ignoreCase: true, out var day))
            {
                errors.Add(line);

                continue;
            }

            foreach (var window in line.Substring(space + 1).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (TryReadWindow(window, out var start, out var end))
                {
                    result.Add(new SchedulingWorkingHours { Day = day, Start = start, End = end });
                }
                else
                {
                    errors.Add(line);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Writes date overrides, one per line, such as <c>2026-12-24 closed</c> or <c>2026-12-31 09:00-12:00</c>.
    /// </summary>
    /// <param name="overrides">The overrides.</param>
    /// <returns>The text.</returns>
    public static string WriteOverrides(IEnumerable<SchedulingDateOverride> overrides)
    {
        return string.Join(Environment.NewLine, overrides.OrderBy(item => item.Date).Select(item =>
            item.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " " + (item.Closed ? "closed" : $"{item.Start}-{item.End}")));
    }

    /// <summary>
    /// Reads date overrides written by <see cref="WriteOverrides"/>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="errors">Lines that could not be read.</param>
    /// <returns>The overrides.</returns>
    public static List<SchedulingDateOverride> ReadOverrides(string text, IList<string> errors)
    {
        var result = new List<SchedulingDateOverride>();

        foreach (var line in Lines(text))
        {
            var parts = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (parts.Length != 2 || !DateTime.TryParseExact(parts[0], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                errors.Add(line);

                continue;
            }

            if (parts[1].Equals("closed", StringComparison.OrdinalIgnoreCase))
            {
                result.Add(new SchedulingDateOverride { Date = date, Closed = true });
            }
            else if (TryReadWindow(parts[1], out var start, out var end))
            {
                result.Add(new SchedulingDateOverride { Date = date, Start = start, End = end });
            }
            else
            {
                errors.Add(line);
            }
        }

        return result;
    }

    /// <summary>
    /// Lists a day's open times as UTC start and local label.
    /// </summary>
    /// <param name="slots">The open times.</param>
    /// <param name="timeZoneId">The time zone to show them in.</param>
    /// <returns>The times.</returns>
    public static List<KeyValuePair<string, string>> ToChoices(IEnumerable<AppointmentSlot> slots, string timeZoneId)
    {
        return slots.Select(slot => new KeyValuePair<string, string>(
            slot.StartUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            CalendarTime.ToLocal(slot.StartUtc, timeZoneId).ToString("t", CultureInfo.CurrentCulture) + (slot.Remaining > 1 ? $" ({slot.Remaining})" : string.Empty)))
            .ToList();
    }

    private static bool TryReadWindow(string text, out string start, out string end)
    {
        start = null;
        end = null;

        var parts = text.Split('-', StringSplitOptions.TrimEntries);

        if (parts.Length != 2 || !DefaultSchedulingAvailabilityService.TryParse(parts[0], out var from) || !DefaultSchedulingAvailabilityService.TryParse(parts[1], out var to) || to <= from)
        {
            return false;
        }

        start = parts[0];
        end = parts[1];

        return true;
    }

    private static string[] Lines(string text)
    {
        return (text ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
