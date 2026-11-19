using System.Globalization;
using System.Text;
using CrestApps.OrchardCore.BusinessHours.Models;

namespace CrestApps.OrchardCore.BusinessHours.Core.Services;

/// <summary>
/// Reads and writes the date exceptions of a schedule as the text the editor shows: one date per line, in
/// <c>yyyy-MM-dd</c>, then optional special hours (<c>09:00-13:00</c>), then an optional name. A date without hours is
/// closed all day. Pure.
/// </summary>
/// <example>
/// <code>
/// 2026-12-24 09:00-13:00 Christmas Eve
/// 2026-12-25 Christmas Day
/// </code>
/// </example>
public static class HoursDateExceptionText
{
    private const string DateFormat = "yyyy-MM-dd";

    /// <summary>
    /// Writes date exceptions, one per line, in date order.
    /// </summary>
    /// <param name="exceptions">The date exceptions.</param>
    /// <returns>The text, or <see langword="null"/> when there are none.</returns>
    public static string Format(IEnumerable<HoursDateException> exceptions)
    {
        var builder = new StringBuilder();

        foreach (var exception in (exceptions ?? []).Where(exception => exception is not null).OrderBy(exception => exception.Date))
        {
            if (builder.Length > 0)
            {
                builder.Append(Environment.NewLine);
            }

            builder.Append(exception.Date.ToString(DateFormat, CultureInfo.InvariantCulture));

            if (!exception.Closed)
            {
                builder.Append(' ')
                    .Append(WeeklyHoursRows.FormatMinute(exception.OpenMinute))
                    .Append('-')
                    .Append(WeeklyHoursRows.FormatMinute(exception.CloseMinute));
            }

            if (!string.IsNullOrWhiteSpace(exception.Name))
            {
                builder.Append(' ').Append(exception.Name.Trim());
            }
        }

        return builder.Length == 0 ? null : builder.ToString();
    }

    /// <summary>
    /// Reads date exceptions. Blank lines are skipped; a date given twice keeps its last line.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="invalidLines">Receives the one-based number of each line that does not start with a date or has
    /// hours that cannot be read.</param>
    /// <returns>The date exceptions, in date order.</returns>
    public static IList<HoursDateException> Parse(string text, ICollection<int> invalidLines = null)
    {
        var exceptions = new SortedDictionary<DateOnly, HoursDateException>();

        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].Trim();

            if (line.Length == 0)
            {
                continue;
            }

            if (TryParseLine(line, out var exception))
            {
                exceptions[exception.Date] = exception;
            }
            else
            {
                invalidLines?.Add(index + 1);
            }
        }

        return [.. exceptions.Values];
    }

    private static bool TryParseLine(string line, out HoursDateException exception)
    {
        exception = null;

        var parts = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (!DateOnly.TryParseExact(parts[0], DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return false;
        }

        exception = new HoursDateException
        {
            Date = date,
            Closed = true,
        };

        var rest = parts.Length > 1 ? parts[1] : null;

        if (string.IsNullOrEmpty(rest))
        {
            return true;
        }

        var restParts = rest.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var hours = restParts[0].Split('-', StringSplitOptions.TrimEntries);

        // The first word is hours when it looks like "HH:mm-HH:mm"; otherwise the whole rest is the name.
        if (hours.Length == 2 && hours[0].Contains(':', StringComparison.Ordinal) && hours[1].Contains(':', StringComparison.Ordinal))
        {
            if (!WeeklyHoursRows.TryParseMinute(hours[0], out var open) || !WeeklyHoursRows.TryParseMinute(hours[1], out var close))
            {
                exception = null;

                return false;
            }

            exception.Closed = false;
            exception.OpenMinute = open;
            exception.CloseMinute = close;
            exception.Name = restParts.Length > 1 ? restParts[1] : null;
        }
        else
        {
            exception.Name = rest;
        }

        return true;
    }
}
