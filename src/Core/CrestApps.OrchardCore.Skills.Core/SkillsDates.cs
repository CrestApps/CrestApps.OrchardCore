namespace CrestApps.OrchardCore.Skills.Core;

/// <summary>
/// Turns calendar days into the <c>yyyyMMdd</c> numbers the index tables store, so day comparisons are plain number
/// comparisons on every database and no driver shifts a date by a time zone.
/// </summary>
public static class SkillsDates
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
