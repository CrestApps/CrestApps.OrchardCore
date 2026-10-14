using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Subscriptions.Models;

namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

/// <summary>
/// Works out an installment plan's schedule: what each payment after the down payment is, and when it falls due.
/// </summary>
public static class InstallmentScheduleCalculator
{
    /// <summary>
    /// Splits <c>total − downPayment</c> into <paramref name="count"/> payments at the currency's precision. The
    /// payments are equal except the last, which absorbs the rounding so the schedule always adds up to the total
    /// exactly: a schedule that is a cent short is a cent the merchant never collects.
    /// </summary>
    /// <param name="total">The total paid over the life of the plan, the down payment included.</param>
    /// <param name="downPayment">The amount collected up front.</param>
    /// <param name="count">The number of payments after the down payment.</param>
    /// <param name="currency">The ISO-4217 currency.</param>
    /// <returns>The payment amounts in order, or an empty list when the inputs cannot make a schedule.</returns>
    public static IReadOnlyList<decimal> SplitAmounts(decimal total, decimal downPayment, int count, string currency)
    {
        var remaining = CurrencyScale.Round(total - downPayment, currency);

        if (count <= 0 || remaining <= 0m)
        {
            return [];
        }

        var decimals = CurrencyScale.GetDecimalPlaces(currency);
        var factor = (decimal)Math.Pow(10, decimals);

        // Rounded down, so the earlier payments never ask for more than an even share and the last one is the
        // only one that differs (by less than one minor unit per payment).
        var regular = Math.Floor(remaining / count * factor) / factor;

        var amounts = new decimal[count];

        for (var index = 0; index < count - 1; index++)
        {
            amounts[index] = regular;
        }

        amounts[count - 1] = CurrencyScale.Round(remaining - (regular * (count - 1)), currency);

        return amounts;
    }

    /// <summary>
    /// Returns when payment <paramref name="number"/> (1-based) falls due.
    /// </summary>
    /// <param name="firstDueUtc">When the first payment falls due.</param>
    /// <param name="frequency">How often payments fall due.</param>
    /// <param name="number">The 1-based payment number.</param>
    /// <remarks>
    /// Months are always counted from the first due date, never from the previous payment, so a plan that starts on
    /// the 31st falls due on the last day of a short month and returns to the 31st afterwards instead of drifting to
    /// the 28th for good.
    /// </remarks>
    public static DateTime GetDueDate(DateTime firstDueUtc, InstallmentFrequency frequency, int number)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);

        var steps = number - 1;

        return frequency switch
        {
            InstallmentFrequency.Weekly => firstDueUtc.AddDays(7 * steps),
            InstallmentFrequency.BiWeekly => firstDueUtc.AddDays(14 * steps),
            InstallmentFrequency.Monthly => firstDueUtc.AddMonths(steps),
            InstallmentFrequency.Quarterly => firstDueUtc.AddMonths(3 * steps),
            _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, null),
        };
    }

    /// <summary>
    /// Builds the scheduled payments (numbers 1 to <paramref name="count"/>) of a plan.
    /// </summary>
    /// <param name="total">The total paid over the life of the plan, the down payment included.</param>
    /// <param name="downPayment">The amount collected up front.</param>
    /// <param name="count">The number of payments after the down payment.</param>
    /// <param name="frequency">How often they fall due.</param>
    /// <param name="firstDueUtc">When the first falls due.</param>
    /// <param name="currency">The ISO-4217 currency.</param>
    public static IReadOnlyList<InstallmentPlanPayment> BuildSchedule(
        decimal total,
        decimal downPayment,
        int count,
        InstallmentFrequency frequency,
        DateTime firstDueUtc,
        string currency)
    {
        var amounts = SplitAmounts(total, downPayment, count, currency);

        return amounts
            .Select((amount, index) => new InstallmentPlanPayment
            {
                Number = index + 1,
                Amount = amount,
                DueUtc = GetDueDate(firstDueUtc, frequency, index + 1),
                Status = InstallmentPaymentStatus.Scheduled,
            })
            .ToArray();
    }
}
