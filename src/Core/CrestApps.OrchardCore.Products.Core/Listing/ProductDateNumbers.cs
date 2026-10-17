using System.Globalization;

namespace CrestApps.OrchardCore.Products.Core.Listing;

/// <summary>
/// Dates as the numbers the product search ranges over: <c>2026-01-15</c> is <c>20260115</c>.
/// </summary>
/// <remarks>
/// A date indexed as a number reuses everything numbers already have: range filters, the lowest and highest value,
/// and sorting. Only reading an address, writing one and showing a date need to know the number is a date.
/// </remarks>
public static class ProductDateNumbers
{
    /// <summary>
    /// The number of a date, ignoring its time of day.
    /// </summary>
    public static decimal ToNumber(DateTime date)
        => (date.Year * 10_000) + (date.Month * 100) + date.Day;

    /// <summary>
    /// The date a number stands for, or <see langword="null"/> when it is not a valid date.
    /// </summary>
    public static DateOnly? ToDate(decimal? number)
    {
        if (number is null || number < 10_101 || number != decimal.Truncate(number.Value))
        {
            return null;
        }

        var value = (int)number.Value;
        var (year, month, day) = (value / 10_000, value / 100 % 100, value % 100);

        return year is >= 1 and <= 9999 && month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(year, month)
            ? new DateOnly(year, month, day)
            : null;
    }

    /// <summary>
    /// Reads a date from an address, written <c>2026-01-15</c>.
    /// </summary>
    public static decimal? Parse(string value)
        => DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? ToNumber(date.ToDateTime(TimeOnly.MinValue))
            : null;

    /// <summary>
    /// Writes a date for an address or a date input, as <c>2026-01-15</c>.
    /// </summary>
    public static string ToIso(decimal? number)
        => ToDate(number)?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>
    /// Ready-made ranges over dates: one per year from the earliest to the latest, newest first, when they span more
    /// than one year and no more than <paramref name="maxYears"/>.
    /// </summary>
    public static IReadOnlyList<(decimal? Min, decimal? Max)> GetYearBuckets(decimal min, decimal max, int maxYears = 8)
    {
        if (ToDate(min) is not { } first || ToDate(max) is not { } last || last.Year <= first.Year || last.Year - first.Year >= maxYears)
        {
            return [];
        }

        return Enumerable.Range(first.Year, last.Year - first.Year + 1)
            .Reverse()
            .Select(year => ((decimal?)((year * 10_000) + 101), (decimal?)((year * 10_000) + 1231)))
            .ToArray();
    }

    /// <summary>
    /// Whether a range covers exactly one calendar year, so it reads as that year.
    /// </summary>
    public static bool IsWholeYear(decimal? min, decimal? max, out int year)
    {
        year = 0;

        if (ToDate(min) is not { } first || ToDate(max) is not { } last ||
            first.Year != last.Year || first.Month != 1 || first.Day != 1 || last.Month != 12 || last.Day != 31)
        {
            return false;
        }

        year = first.Year;

        return true;
    }
}
