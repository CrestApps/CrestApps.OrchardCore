using CrestApps.OrchardCore.Payments.Models;

namespace CrestApps.OrchardCore.Subscriptions.Core;

/// <summary>
/// Provides billing-cycle date calculations that are shared across the subscription flow so that
/// every payment provider and view computes the next billing date in exactly the same way.
/// </summary>
public static class BillingSchedule
{
    /// <summary>
    /// Advances <paramref name="from"/> by a single billing cycle of the given <paramref name="durationType"/>
    /// and <paramref name="duration"/>.
    /// </summary>
    /// <param name="from">The starting date, typically the moment the subscription became active.</param>
    /// <param name="durationType">The unit of the billing cycle.</param>
    /// <param name="duration">The number of <paramref name="durationType"/> units in a single billing cycle. Must be greater than zero.</param>
    /// <returns>The date on which the next billing cycle begins.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="duration"/> is not greater than zero or when <paramref name="durationType"/> is not supported.
    /// </exception>
    public static DateTime GetNextBillingDate(DateTime from, DurationType durationType, int duration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duration, 0);

        return durationType switch
        {
            DurationType.Day => from.AddDays(duration),
            DurationType.Week => from.AddDays(duration * 7),
            DurationType.Month => from.AddMonths(duration),
            DurationType.Year => from.AddYears(duration),
            _ => throw new ArgumentOutOfRangeException(nameof(durationType), durationType, "The duration type is not supported."),
        };
    }
}
