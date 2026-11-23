using System.Globalization;

namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// The well-known filing frequencies of a tax registration. A frequency is stored as text so a pack can add its own,
/// but the built-in period rules know only these.
/// </summary>
public static class TaxFilingFrequencies
{
    /// <summary>
    /// One return a month.
    /// </summary>
    public const string Monthly = "Monthly";

    /// <summary>
    /// One return a calendar quarter.
    /// </summary>
    public const string Quarterly = "Quarterly";

    /// <summary>
    /// One return a calendar year.
    /// </summary>
    public const string Annual = "Annual";

    /// <summary>
    /// Gets the frequencies in the order they are offered.
    /// </summary>
    public static readonly string[] All =
    [
        Monthly,
        Quarterly,
        Annual,
    ];

    /// <summary>
    /// Gets the number of months in a period of a frequency.
    /// </summary>
    /// <param name="frequency">The frequency.</param>
    /// <returns>The months: 1, 3 or 12.</returns>
    /// <exception cref="ArgumentException">The frequency is not one of the well-known values.</exception>
    public static int MonthsOf(string frequency)
        => frequency switch
        {
            Monthly => 1,
            Quarterly => 3,
            Annual => 12,
            _ => throw new ArgumentException($"'{frequency}' is not a known filing frequency.", nameof(frequency)),
        };

    /// <summary>
    /// Gets whether a value is one of the well-known frequencies.
    /// </summary>
    /// <param name="frequency">The value.</param>
    /// <returns><see langword="true"/> when it is a known frequency.</returns>
    public static bool IsKnown(string frequency)
        => frequency is Monthly or Quarterly or Annual;
}

/// <summary>
/// How often a registration files its returns and when each return is due: the day of a month after the period ends,
/// moved to the next business day when it falls on a weekend. For example, the 20th of the month after a month or a
/// quarter, or the last day of February after a year.
/// </summary>
public sealed class TaxFilingSchedule
{
    /// <summary>
    /// Gets or sets the filing frequency (<see cref="TaxFilingFrequencies"/>).
    /// </summary>
    public string Frequency { get; set; } = TaxFilingFrequencies.Monthly;

    /// <summary>
    /// Gets or sets how many months after the month the period ends in the return is due: 1 for the month after.
    /// </summary>
    public int DueMonthsAfterPeriod { get; set; } = 1;

    /// <summary>
    /// Gets or sets the day of that month the return is due, 1 to 31. A day past the month's end is its last day.
    /// </summary>
    public int DueDay { get; set; } = 20;

    /// <summary>
    /// Gets or sets a value indicating whether a due date on a Saturday or a Sunday moves to the Monday after.
    /// </summary>
    public bool MoveToNextBusinessDay { get; set; } = true;

    /// <summary>
    /// Gets or sets the name of the return form (an <c>ITaxReturnForm</c>) the periodic returns use, or
    /// <see langword="null"/> for the built-in generic form.
    /// </summary>
    public string Form { get; set; }

    /// <summary>
    /// Gets or sets the name of the return form of a yearly reconciliation return filed besides the periodic returns,
    /// such as a state's annual return of a monthly filer, or <see langword="null"/> when there is none. It is ignored
    /// for an annual frequency.
    /// </summary>
    public string AnnualForm { get; set; }

    /// <summary>
    /// Gets or sets the month after the year an annual reconciliation return is due in: 1 for January.
    /// </summary>
    public int AnnualDueMonthsAfterYear { get; set; } = 1;

    /// <summary>
    /// Gets or sets the day of that month the annual reconciliation return is due.
    /// </summary>
    public int AnnualDueDay { get; set; } = 31;

    /// <summary>
    /// Gets the period of the schedule's frequency that holds a day.
    /// </summary>
    /// <param name="day">The day.</param>
    /// <returns>The period.</returns>
    public TaxReturnPeriod PeriodOf(DateOnly day)
        => TaxReturnPeriod.For(Frequency, day);

    /// <summary>
    /// Gets the day a return of a period is due.
    /// </summary>
    /// <param name="period">The period.</param>
    /// <param name="annualReconciliation">Whether the return is the annual reconciliation return.</param>
    /// <returns>The due date.</returns>
    public DateOnly DueDateOf(TaxReturnPeriod period, bool annualReconciliation = false)
    {
        ArgumentNullException.ThrowIfNull(period);

        var monthsAfter = annualReconciliation ? AnnualDueMonthsAfterYear : DueMonthsAfterPeriod;
        var day = annualReconciliation ? AnnualDueDay : DueDay;
        var month = new DateOnly(period.End.Year, period.End.Month, 1).AddMonths(Math.Max(0, monthsAfter));
        var due = new DateOnly(month.Year, month.Month, Math.Clamp(day, 1, DateTime.DaysInMonth(month.Year, month.Month)));

        if (!MoveToNextBusinessDay)
        {
            return due;
        }

        return due.DayOfWeek switch
        {
            DayOfWeek.Saturday => due.AddDays(2),
            DayOfWeek.Sunday => due.AddDays(1),
            _ => due,
        };
    }

    /// <summary>
    /// Copies the schedule.
    /// </summary>
    /// <returns>The copy.</returns>
    public TaxFilingSchedule Clone()
        => (TaxFilingSchedule)MemberwiseClone();
}

/// <summary>
/// A period a return covers: a month, a quarter or a year.
/// </summary>
/// <param name="Frequency">The frequency (<see cref="TaxFilingFrequencies"/>).</param>
/// <param name="Start">The first day.</param>
/// <param name="End">The last day.</param>
public sealed record TaxReturnPeriod(string Frequency, DateOnly Start, DateOnly End)
{
    /// <summary>
    /// Gets the key of the period, such as <c>2026-10</c>, <c>2026-Q3</c> or <c>2026</c>.
    /// </summary>
    public string Key
        => Frequency switch
        {
            TaxFilingFrequencies.Monthly => Start.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            TaxFilingFrequencies.Quarterly => string.Create(CultureInfo.InvariantCulture, $"{Start.Year}-Q{((Start.Month - 1) / 3) + 1}"),
            _ => Start.Year.ToString(CultureInfo.InvariantCulture),
        };

    /// <summary>
    /// Gets the period of a frequency that holds a day.
    /// </summary>
    /// <param name="frequency">The frequency.</param>
    /// <param name="day">The day.</param>
    /// <returns>The period.</returns>
    public static TaxReturnPeriod For(string frequency, DateOnly day)
    {
        var months = TaxFilingFrequencies.MonthsOf(frequency);
        var startMonth = (((day.Month - 1) / months) * months) + 1;
        var start = new DateOnly(day.Year, startMonth, 1);

        return new TaxReturnPeriod(frequency, start, start.AddMonths(months).AddDays(-1));
    }

    /// <summary>
    /// Reads a key back into its period.
    /// </summary>
    /// <param name="key">The key, such as <c>2026-10</c>, <c>2026-Q3</c> or <c>2026</c>.</param>
    /// <returns>The period, or <see langword="null"/> when the key is not one.</returns>
    public static TaxReturnPeriod Parse(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var styles = NumberStyles.None;
        var culture = CultureInfo.InvariantCulture;

        if (key.Length == 4 && int.TryParse(key, styles, culture, out var year) && year is > 1900 and < 3000)
        {
            return For(TaxFilingFrequencies.Annual, new DateOnly(year, 1, 1));
        }

        if (key.Length != 7 || key[4] != '-' || !int.TryParse(key[..4], styles, culture, out year) || year is <= 1900 or >= 3000)
        {
            return null;
        }

        if (key[5] == 'Q')
        {
            return key[6] is >= '1' and <= '4'
                ? For(TaxFilingFrequencies.Quarterly, new DateOnly(year, ((key[6] - '1') * 3) + 1, 1))
                : null;
        }

        return int.TryParse(key[5..], styles, culture, out var month) && month is >= 1 and <= 12
            ? For(TaxFilingFrequencies.Monthly, new DateOnly(year, month, 1))
            : null;
    }

    /// <summary>
    /// Gets the period after this one.
    /// </summary>
    /// <returns>The next period.</returns>
    public TaxReturnPeriod Next()
        => For(Frequency, End.AddDays(1));

    /// <summary>
    /// Lists the periods of a frequency that end in a span, in order.
    /// </summary>
    /// <param name="frequency">The frequency.</param>
    /// <param name="from">The first day of the span.</param>
    /// <param name="to">The last day of the span.</param>
    /// <returns>The periods.</returns>
    public static IReadOnlyList<TaxReturnPeriod> Between(string frequency, DateOnly from, DateOnly to)
    {
        var periods = new List<TaxReturnPeriod>();

        if (to < from)
        {
            return periods;
        }

        for (var period = For(frequency, from); period.Start <= to; period = period.Next())
        {
            if (period.End >= from && period.End <= to)
            {
                periods.Add(period);
            }
        }

        return periods;
    }
}
