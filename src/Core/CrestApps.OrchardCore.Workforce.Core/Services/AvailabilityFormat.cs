using System.Globalization;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Reads and writes a worker's weekly availability as lines of text, one day per line, such as
/// <c>Tuesday 08:00-12:00 preferred, 13:00-17:00</c> or <c>Saturday unavailable</c>.
/// </summary>
internal static class AvailabilityFormat
{
    public static IList<AvailabilityWindow> Read(string text, ICollection<string> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        var windows = new List<AvailabilityWindow>();

        foreach (var rawLine in (text ?? string.Empty).Split('\n'))
        {
            var line = rawLine.Trim();

            if (line.Length == 0)
            {
                continue;
            }

            var space = line.IndexOf(' ', StringComparison.Ordinal);
            var dayText = space < 0 ? line : line.Substring(0, space);

            if (!Enum.TryParse<DayOfWeek>(dayText, ignoreCase: true, out var day) || int.TryParse(dayText, out _))
            {
                errors.Add(line);

                continue;
            }

            var rest = space < 0 ? string.Empty : line.Substring(space + 1).Trim();

            if (rest.Equals("unavailable", StringComparison.OrdinalIgnoreCase))
            {
                windows.Add(new AvailabilityWindow { Day = day, Preference = AvailabilityPreference.Unavailable });

                continue;
            }

            var parsed = new List<AvailabilityWindow>();
            var valid = rest.Length > 0;

            foreach (var part in rest.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var words = part.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var range = words[0].Split('-');
                var preference = AvailabilityPreference.Available;

                if (words.Length > 1)
                {
                    if (words.Length > 2 || !Enum.TryParse(words[1], ignoreCase: true, out preference) || int.TryParse(words[1], out _))
                    {
                        valid = false;

                        break;
                    }
                }

                if (range.Length != 2 || !TryParseTime(range[0], out var start) || !TryParseTime(range[1], out var end) || start == end)
                {
                    valid = false;

                    break;
                }

                parsed.Add(new AvailabilityWindow { Day = day, Start = start, End = end, Preference = preference });
            }

            if (!valid)
            {
                errors.Add(line);

                continue;
            }

            windows.AddRange(parsed);
        }

        return windows;
    }

    public static string Write(IEnumerable<AvailabilityWindow> windows)
    {
        var lines = new List<string>();

        foreach (var day in (windows ?? []).GroupBy(window => window.Day).OrderBy(group => ((int)group.Key + 6) % 7))
        {
            if (day.Any(window => window.Preference == AvailabilityPreference.Unavailable && window.Start == window.End))
            {
                lines.Add($"{day.Key} unavailable");

                continue;
            }

            var parts = day.OrderBy(window => window.Start).Select(window => window.Preference == AvailabilityPreference.Available
                ? $"{window.Start:HH\\:mm}-{window.End:HH\\:mm}"
                : $"{window.Start:HH\\:mm}-{window.End:HH\\:mm} {window.Preference.ToString().ToLowerInvariant()}");

            lines.Add($"{day.Key} {string.Join(", ", parts)}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static bool TryParseTime(string text, out TimeOnly time)
    {
        return TimeOnly.TryParseExact(text.Trim(), ["HH:mm", "H:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
    }
}
