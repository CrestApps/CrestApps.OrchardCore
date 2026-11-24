namespace CrestApps.OrchardCore.Staff.Core;

/// <summary>
/// Helpers for the values Staff indexes.
/// </summary>
public static class StaffKeys
{
    /// <summary>
    /// Normalizes an employee number or other code for matching: trimmed and upper case, or <see langword="null"/> when empty.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The normalized value.</returns>
    public static string Normalize(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    /// <summary>
    /// Turns a day into a sortable number such as 20261111, or <see langword="null"/>.
    /// </summary>
    /// <param name="date">The day.</param>
    /// <returns>The key.</returns>
    public static int? ToKey(DateOnly? date)
        => date is null ? null : ToKey(date.Value);

    /// <summary>
    /// Turns a day into a sortable number such as 20261111.
    /// </summary>
    /// <param name="date">The day.</param>
    /// <returns>The key.</returns>
    public static int ToKey(DateOnly date)
        => (date.Year * 10000) + (date.Month * 100) + date.Day;

    /// <summary>
    /// Turns a key made by <see cref="ToKey(DateOnly)"/> back into a day.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The day, or <see langword="null"/> when the key isn't a day.</returns>
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
