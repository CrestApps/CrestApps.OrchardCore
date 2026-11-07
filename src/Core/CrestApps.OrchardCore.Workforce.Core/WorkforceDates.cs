namespace CrestApps.OrchardCore.Workforce.Core;

/// <summary>
/// Turns calendar days into the <c>yyyyMMdd</c> numbers the index tables store.
/// </summary>
public static class WorkforceDates
{
    /// <summary>
    /// Gets the index number of a day.
    /// </summary>
    /// <param name="date">The day, or <see langword="null"/>.</param>
    /// <returns>The number, or <see langword="null"/> for no day.</returns>
    public static int? ToKey(DateOnly? date)
    {
        return date is null
            ? null
            : ToKey(date.Value);
    }

    /// <summary>
    /// Gets the index number of a day.
    /// </summary>
    /// <param name="date">The day.</param>
    /// <returns>The number.</returns>
    public static int ToKey(DateOnly date)
    {
        return (date.Year * 10000) + (date.Month * 100) + date.Day;
    }

    /// <summary>
    /// Gets the day of an index number.
    /// </summary>
    /// <param name="key">The <c>yyyyMMdd</c> number, or <see langword="null"/>.</param>
    /// <returns>The day, or <see langword="null"/> for no day or a number that isn't a day.</returns>
    public static DateOnly? FromKey(int? key)
    {
        if (key is not { } value || value <= 0)
        {
            return null;
        }

        var (year, month, day) = (value / 10000, value / 100 % 100, value % 100);

        return year is >= 1 and <= 9999 && month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(year, month)
            ? new DateOnly(year, month, day)
            : null;
    }
}