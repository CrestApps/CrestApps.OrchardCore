namespace CrestApps.OrchardCore.Payments.Models;

/// <summary>
/// Date arithmetic for billing cycles, trials and other spans measured in a <see cref="DurationType"/>.
/// </summary>
public static class DurationTypeExtensions
{
    /// <summary>
    /// Advances a date by <paramref name="count"/> units of <paramref name="durationType"/>. A count below one counts
    /// as one, so a cycle always moves the date forward.
    /// </summary>
    /// <param name="durationType">The unit of the span.</param>
    /// <param name="from">The date to advance from.</param>
    /// <param name="count">How many units to advance by.</param>
    /// <returns>The advanced date.</returns>
    public static DateTime AddTo(this DurationType durationType, DateTime from, int count)
    {
        count = Math.Max(1, count);

        return durationType switch
        {
            DurationType.Day => from.AddDays(count),
            DurationType.Week => from.AddDays(count * 7),
            DurationType.Year => from.AddYears(count),
            _ => from.AddMonths(count),
        };
    }
}
