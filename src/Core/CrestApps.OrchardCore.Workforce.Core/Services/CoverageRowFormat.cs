using System.Globalization;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Reads and writes the rows of a coverage requirement as lines of text: a weekday or a date, a time range, then the
/// minimum, the target and an optional maximum, such as <c>Monday 09:00-17:00 1 2</c> or
/// <c>2026-12-24 08:00-12:00 0 1 3</c>. An end of <c>24:00</c> means midnight.
/// </summary>
internal static class CoverageRowFormat
{
    private const string DateFormat = "yyyy-MM-dd";

    public static IList<CoverageRow> Read(string text, ICollection<string> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        var rows = new List<CoverageRow>();

        foreach (var rawLine in (text ?? string.Empty).Split('\n'))
        {
            var line = rawLine.Trim();

            if (line.Length == 0)
            {
                continue;
            }

            var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var row = new CoverageRow();

            if (words.Length is < 4 or > 5 || !TryReadWhen(words[0], row) || !TryReadRange(words[1], row) || !TryReadCounts(words.Skip(2).ToArray(), row))
            {
                errors.Add(line);

                continue;
            }

            rows.Add(row);
        }

        return rows;
    }

    public static string Write(IEnumerable<CoverageRow> rows)
    {
        return string.Join(Environment.NewLine, (rows ?? []).Select(row =>
        {
            var when = row.Date.HasValue ? row.Date.Value.ToString(DateFormat, CultureInfo.InvariantCulture) : row.Day?.ToString();
            var end = row.End == TimeOnly.MinValue ? "24:00" : row.End.ToString("HH:mm", CultureInfo.InvariantCulture);
            var counts = row.Maximum.HasValue ? $"{row.Minimum} {row.Target} {row.Maximum}" : $"{row.Minimum} {row.Target}";

            return $"{when} {row.Start.ToString("HH:mm", CultureInfo.InvariantCulture)}-{end} {counts}";
        }));
    }

    private static bool TryReadWhen(string text, CoverageRow row)
    {
        if (DateOnly.TryParseExact(text, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            row.Date = date;

            return true;
        }

        if (int.TryParse(text, out _))
        {
            return false;
        }

        foreach (var day in Enum.GetValues<DayOfWeek>())
        {
            var name = day.ToString();

            if (string.Equals(name, text, StringComparison.OrdinalIgnoreCase) || (text.Length == 3 && name.StartsWith(text, StringComparison.OrdinalIgnoreCase)))
            {
                row.Day = day;

                return true;
            }
        }

        return false;
    }

    private static bool TryReadRange(string text, CoverageRow row)
    {
        var range = text.Split('-');

        if (range.Length != 2 || !TryParseTime(range[0], out var start))
        {
            return false;
        }

        TimeOnly end;

        if (range[1] == "24:00")
        {
            end = TimeOnly.MinValue;
        }
        else if (!TryParseTime(range[1], out end) || end <= start)
        {
            return false;
        }

        row.Start = start;
        row.End = end;

        return true;
    }

    private static bool TryReadCounts(string[] words, CoverageRow row)
    {
        if (!int.TryParse(words[0], NumberStyles.None, CultureInfo.InvariantCulture, out var minimum)
            || !int.TryParse(words[1], NumberStyles.None, CultureInfo.InvariantCulture, out var target))
        {
            return false;
        }

        row.Minimum = minimum;
        row.Target = target;

        if (words.Length == 3)
        {
            if (!int.TryParse(words[2], NumberStyles.None, CultureInfo.InvariantCulture, out var maximum))
            {
                return false;
            }

            row.Maximum = maximum;
        }

        return true;
    }

    private static bool TryParseTime(string text, out TimeOnly time)
        => TimeOnly.TryParseExact(text.Trim(), ["HH:mm", "H:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
}
