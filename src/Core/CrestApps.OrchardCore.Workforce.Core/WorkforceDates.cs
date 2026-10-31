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
}
