using System.Globalization;
using System.Text;
using NodaTime;

namespace CrestApps.OrchardCore.Calendars.Core.Recurrence;

/// <summary>
/// An RFC 5545 recurrence rule (<c>RRULE</c>). Supports <c>FREQ</c> (DAILY, WEEKLY, MONTHLY, YEARLY), <c>INTERVAL</c>,
/// <c>COUNT</c>, <c>UNTIL</c>, <c>BYDAY</c> (with ordinals), <c>BYMONTHDAY</c>, <c>BYMONTH</c>, <c>BYSETPOS</c> and <c>WKST</c>.
/// Occurrences start at the event's own time of day, so <c>BYHOUR</c>, <c>BYMINUTE</c> and <c>BYSECOND</c> are not supported.
/// </summary>
public sealed class RecurrenceRule
{
    private static readonly Dictionary<string, IsoDayOfWeek> _days = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MO"] = IsoDayOfWeek.Monday,
        ["TU"] = IsoDayOfWeek.Tuesday,
        ["WE"] = IsoDayOfWeek.Wednesday,
        ["TH"] = IsoDayOfWeek.Thursday,
        ["FR"] = IsoDayOfWeek.Friday,
        ["SA"] = IsoDayOfWeek.Saturday,
        ["SU"] = IsoDayOfWeek.Sunday,
    };

    /// <summary>
    /// Gets or sets how often the rule repeats.
    /// </summary>
    public RecurrenceFrequency Frequency { get; set; }

    /// <summary>
    /// Gets or sets every how many periods the rule repeats.
    /// </summary>
    public int Interval { get; set; } = 1;

    /// <summary>
    /// Gets or sets how many occurrences the rule makes, or <see langword="null"/>.
    /// </summary>
    public int? Count { get; set; }

    /// <summary>
    /// Gets or sets the last moment an occurrence may start, or <see langword="null"/>. Inclusive.
    /// </summary>
    public DateTime? Until { get; set; }

    /// <summary>
    /// Gets or sets whether <see cref="Until"/> is a UTC moment; otherwise it is a wall-clock time in the event's time zone.
    /// </summary>
    public bool UntilIsUtc { get; set; }

    /// <summary>
    /// Gets or sets whether <see cref="Until"/> is a date, the last day an occurrence may fall on.
    /// </summary>
    public bool UntilIsDate { get; set; }

    /// <summary>
    /// Gets the <c>BYDAY</c> entries.
    /// </summary>
    public IList<RecurrenceWeekday> ByDay { get; } = [];

    /// <summary>
    /// Gets the <c>BYMONTHDAY</c> entries: 1 to 31, or -1 to -31 counted from the end of the month.
    /// </summary>
    public IList<int> ByMonthDay { get; } = [];

    /// <summary>
    /// Gets the <c>BYMONTH</c> entries, 1 to 12.
    /// </summary>
    public IList<int> ByMonth { get; } = [];

    /// <summary>
    /// Gets the <c>BYSETPOS</c> entries: which of each period's candidates to keep, counted from the end when negative.
    /// </summary>
    public IList<int> BySetPosition { get; } = [];

    /// <summary>
    /// Gets or sets the day a week starts on.
    /// </summary>
    public IsoDayOfWeek WeekStart { get; set; } = IsoDayOfWeek.Monday;

    /// <summary>
    /// Gets whether the rule ends, by a count or an end date.
    /// </summary>
    public bool IsFinite => Count.HasValue || Until.HasValue;

    /// <summary>
    /// Parses a rule, with or without the <c>RRULE:</c> prefix.
    /// </summary>
    /// <param name="value">The rule.</param>
    /// <returns>The parsed rule.</returns>
    /// <exception cref="FormatException">The rule is not valid or uses a part that isn't supported.</exception>
    public static RecurrenceRule Parse(string value)
    {
        if (!TryParse(value, out var rule, out var error))
        {
            throw new FormatException(error);
        }

        return rule;
    }

    /// <summary>
    /// Tries to parse a rule, with or without the <c>RRULE:</c> prefix.
    /// </summary>
    /// <param name="value">The rule.</param>
    /// <param name="rule">The parsed rule.</param>
    /// <param name="error">Why the rule isn't valid.</param>
    /// <returns><see langword="true"/> when the rule is valid.</returns>
    public static bool TryParse(string value, out RecurrenceRule rule, out string error)
    {
        rule = null;
        error = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            error = "The recurrence rule is empty.";

            return false;
        }

        var text = value.Trim();

        if (text.StartsWith("RRULE:", StringComparison.OrdinalIgnoreCase))
        {
            text = text.Substring(6);
        }

        var result = new RecurrenceRule();
        var hasFrequency = false;

        foreach (var part in text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = part.IndexOf('=');

            if (separator <= 0)
            {
                error = $"'{part}' is not a NAME=VALUE pair.";

                return false;
            }

            var name = part.Substring(0, separator).ToUpperInvariant();
            var data = part.Substring(separator + 1);

            switch (name)
            {
                case "FREQ":
                    switch (data.ToUpperInvariant())
                    {
                        case "DAILY":
                            result.Frequency = RecurrenceFrequency.Daily;
                            break;
                        case "WEEKLY":
                            result.Frequency = RecurrenceFrequency.Weekly;
                            break;
                        case "MONTHLY":
                            result.Frequency = RecurrenceFrequency.Monthly;
                            break;
                        case "YEARLY":
                            result.Frequency = RecurrenceFrequency.Yearly;
                            break;
                        default:
                            error = $"The frequency '{data}' is not supported.";

                            return false;
                    }

                    hasFrequency = true;
                    break;

                case "INTERVAL":
                    if (!int.TryParse(data, NumberStyles.None, CultureInfo.InvariantCulture, out var interval) || interval < 1)
                    {
                        error = "INTERVAL must be a whole number of at least 1.";

                        return false;
                    }

                    result.Interval = interval;
                    break;

                case "COUNT":
                    if (!int.TryParse(data, NumberStyles.None, CultureInfo.InvariantCulture, out var count) || count < 1)
                    {
                        error = "COUNT must be a whole number of at least 1.";

                        return false;
                    }

                    result.Count = count;
                    break;

                case "UNTIL":
                    if (!TryParseUntil(data, result))
                    {
                        error = $"UNTIL '{data}' is not a date or a date-time.";

                        return false;
                    }

                    break;

                case "BYDAY":
                    foreach (var item in data.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        if (item.Length < 2 || !_days.TryGetValue(item.Substring(item.Length - 2), out var day))
                        {
                            error = $"'{item}' is not a day of the week.";

                            return false;
                        }

                        var ordinal = 0;

                        if (item.Length > 2 && (!int.TryParse(item.AsSpan(0, item.Length - 2), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out ordinal) || ordinal == 0 || ordinal < -53 || ordinal > 53))
                        {
                            error = $"'{item}' has an ordinal that is not between -53 and 53.";

                            return false;
                        }

                        result.ByDay.Add(new RecurrenceWeekday(day, ordinal));
                    }

                    break;

                case "BYMONTHDAY":
                    if (!TryParseNumbers(data, 31, allowNegative: true, result.ByMonthDay))
                    {
                        error = "BYMONTHDAY must list days between -31 and 31, other than 0.";

                        return false;
                    }

                    break;

                case "BYMONTH":
                    if (!TryParseNumbers(data, 12, allowNegative: false, result.ByMonth))
                    {
                        error = "BYMONTH must list months between 1 and 12.";

                        return false;
                    }

                    break;

                case "BYSETPOS":
                    if (!TryParseNumbers(data, 366, allowNegative: true, result.BySetPosition))
                    {
                        error = "BYSETPOS must list positions between -366 and 366, other than 0.";

                        return false;
                    }

                    break;

                case "WKST":
                    if (!_days.TryGetValue(data, out var weekStart))
                    {
                        error = $"'{data}' is not a day of the week.";

                        return false;
                    }

                    result.WeekStart = weekStart;
                    break;

                case "BYHOUR":
                case "BYMINUTE":
                case "BYSECOND":
                case "BYWEEKNO":
                case "BYYEARDAY":
                    error = $"{name} is not supported.";

                    return false;

                default:
                    error = $"'{name}' is not a recurrence rule part.";

                    return false;
            }
        }

        if (!hasFrequency)
        {
            error = "The recurrence rule has no FREQ.";

            return false;
        }

        if (result.Count.HasValue && result.Until.HasValue)
        {
            error = "A recurrence rule can't have both COUNT and UNTIL.";

            return false;
        }

        if (result.ByDay.Any(day => day.Ordinal != 0) && result.Frequency is not (RecurrenceFrequency.Monthly or RecurrenceFrequency.Yearly))
        {
            error = "BYDAY can only have an ordinal for a monthly or yearly rule.";

            return false;
        }

        if (result.ByMonthDay.Count > 0 && result.Frequency == RecurrenceFrequency.Weekly)
        {
            error = "BYMONTHDAY can't be used with a weekly rule.";

            return false;
        }

        rule = result;

        return true;
    }

    /// <summary>
    /// Writes the rule without the <c>RRULE:</c> prefix.
    /// </summary>
    /// <returns>The rule text.</returns>
    public override string ToString()
    {
        var builder = new StringBuilder("FREQ=");

        builder.Append(Frequency switch
        {
            RecurrenceFrequency.Daily => "DAILY",
            RecurrenceFrequency.Weekly => "WEEKLY",
            RecurrenceFrequency.Monthly => "MONTHLY",
            _ => "YEARLY",
        });

        if (Interval > 1)
        {
            builder.Append(";INTERVAL=").Append(Interval.ToString(CultureInfo.InvariantCulture));
        }

        if (Count.HasValue)
        {
            builder.Append(";COUNT=").Append(Count.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (Until.HasValue)
        {
            builder.Append(";UNTIL=").Append(FormatUntil());
        }

        if (ByMonth.Count > 0)
        {
            builder.Append(";BYMONTH=").AppendJoin(',', ByMonth.Select(month => month.ToString(CultureInfo.InvariantCulture)));
        }

        if (ByMonthDay.Count > 0)
        {
            builder.Append(";BYMONTHDAY=").AppendJoin(',', ByMonthDay.Select(day => day.ToString(CultureInfo.InvariantCulture)));
        }

        if (ByDay.Count > 0)
        {
            builder.Append(";BYDAY=").AppendJoin(',', ByDay.Select(FormatWeekday));
        }

        if (BySetPosition.Count > 0)
        {
            builder.Append(";BYSETPOS=").AppendJoin(',', BySetPosition.Select(position => position.ToString(CultureInfo.InvariantCulture)));
        }

        if (WeekStart != IsoDayOfWeek.Monday)
        {
            builder.Append(";WKST=").Append(FormatDay(WeekStart));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Gets the two-letter RFC 5545 code of a day of the week.
    /// </summary>
    /// <param name="day">The day.</param>
    /// <returns>The code, such as <c>MO</c>.</returns>
    public static string FormatDay(IsoDayOfWeek day)
    {
        return _days.First(pair => pair.Value == day).Key;
    }

    private static string FormatWeekday(RecurrenceWeekday weekday)
    {
        return weekday.Ordinal == 0
            ? FormatDay(weekday.Day)
            : weekday.Ordinal.ToString(CultureInfo.InvariantCulture) + FormatDay(weekday.Day);
    }

    private string FormatUntil()
    {
        var until = Until.Value;

        if (UntilIsDate)
        {
            return until.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        }

        return until.ToString("yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture) + (UntilIsUtc ? "Z" : string.Empty);
    }

    private static bool TryParseUntil(string data, RecurrenceRule rule)
    {
        if (data.Length == 8 && DateTime.TryParseExact(data, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            rule.Until = date;
            rule.UntilIsDate = true;
            rule.UntilIsUtc = false;

            return true;
        }

        var isUtc = data.EndsWith('Z') || data.EndsWith('z');
        var local = isUtc ? data.Substring(0, data.Length - 1) : data;

        if (DateTime.TryParseExact(local, "yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime))
        {
            rule.Until = isUtc ? DateTime.SpecifyKind(dateTime, DateTimeKind.Utc) : dateTime;
            rule.UntilIsDate = false;
            rule.UntilIsUtc = isUtc;

            return true;
        }

        return false;
    }

    private static bool TryParseNumbers(string data, int max, bool allowNegative, IList<int> target)
    {
        foreach (var item in data.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!int.TryParse(item, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number) ||
                number == 0 ||
                number > max ||
                number < (allowNegative ? -max : 1))
            {
                return false;
            }

            target.Add(number);
        }

        return target.Count > 0;
    }
}
