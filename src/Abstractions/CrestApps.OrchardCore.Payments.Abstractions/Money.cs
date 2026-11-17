using CrestApps.OrchardCore.Currencies;

namespace CrestApps.OrchardCore.Payments;

/// <summary>
/// Provides safe, currency-aware comparison and rounding for monetary amounts carried as
/// <see cref="decimal"/> values.
///
/// Money is represented as <see cref="decimal"/>, the authoritative type for financial values, so amounts
/// are exact at the currency's own scale. Comparisons are still performed after normalizing the amounts to
/// whole minor units for the relevant currency — the smallest unit any supported gateway settles in — so a
/// value expressed at a finer precision than the currency supports can never make two settlement-equal
/// amounts compare as different, and the result stays deterministic and gateway aligned.
/// </summary>
public static class Money
{
    /// <summary>
    /// Rounds a monetary amount to the precision of the supplied currency using away-from-zero rounding.
    /// </summary>
    /// <param name="amount">The amount expressed in major units.</param>
    /// <param name="currency">The ISO-4217 currency code used to determine precision. Defaults to two decimals when unknown.</param>
    public static decimal Round(decimal amount, string currency = null)
        => Math.Round(amount, CurrencyScale.GetDecimalPlaces(currency), MidpointRounding.AwayFromZero);

    /// <summary>
    /// Converts a monetary amount expressed in major units to whole minor units for the supplied currency.
    /// </summary>
    /// <param name="amount">The amount expressed in major units.</param>
    /// <param name="currency">The ISO-4217 currency code used to determine precision.</param>
    public static long ToMinorUnits(decimal amount, string currency = null)
        => CurrencyScale.ToMinorUnits(amount, currency);

    /// <summary>
    /// Determines whether two monetary amounts are equal once normalized to whole minor units for the currency.
    /// </summary>
    /// <param name="left">The first amount expressed in major units.</param>
    /// <param name="right">The second amount expressed in major units.</param>
    /// <param name="currency">The ISO-4217 currency code used to determine precision.</param>
    public static bool AreEqual(decimal left, decimal right, string currency = null)
        => ToMinorUnits(left, currency) == ToMinorUnits(right, currency);

    /// <summary>
    /// Determines whether two nullable monetary amounts are equal once normalized to whole minor units.
    /// A <see langword="null"/> amount is treated as not equal to any value, including another <see langword="null"/>.
    /// </summary>
    /// <param name="left">The first amount expressed in major units.</param>
    /// <param name="right">The second amount expressed in major units.</param>
    /// <param name="currency">The ISO-4217 currency code used to determine precision.</param>
    public static bool AreEqual(decimal? left, decimal? right, string currency = null)
        => left.HasValue && right.HasValue && AreEqual(left.Value, right.Value, currency);

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="amount"/> is strictly greater than
    /// <paramref name="threshold"/> after both are normalized to whole minor units for the currency.
    /// </summary>
    /// <param name="amount">The amount to test, expressed in major units.</param>
    /// <param name="threshold">The threshold to compare against, expressed in major units.</param>
    /// <param name="currency">The ISO-4217 currency code used to determine precision.</param>
    public static bool IsGreaterThan(decimal amount, decimal threshold, string currency = null)
        => ToMinorUnits(amount, currency) > ToMinorUnits(threshold, currency);

    /// <summary>
    /// Splits an amount into shares in proportion to weights, such as net pay across bank accounts or a cost across
    /// departments. Each share is rounded to the currency's minor unit and the last share with a weight takes what
    /// rounding leaves, so the shares always add up to the amount exactly. A share never has the opposite sign of the
    /// amount.
    /// </summary>
    /// <param name="total">The amount to split, expressed in major units. It may be negative, for a reversal.</param>
    /// <param name="weights">The weight of each share: percentages, amounts or any other proportion. A weight of zero gets nothing.</param>
    /// <param name="currency">The ISO-4217 currency code used to determine precision.</param>
    /// <returns>One share for each weight, in the same order.</returns>
    /// <exception cref="ArgumentException">A weight is negative, or there is an amount to split and no share has a weight.</exception>
    public static decimal[] Allocate(decimal total, IReadOnlyList<decimal> weights, string currency = null)
    {
        ArgumentNullException.ThrowIfNull(weights);

        var shares = new decimal[weights.Count];
        var sum = 0m;
        var last = -1;

        for (var i = 0; i < weights.Count; i++)
        {
            if (weights[i] < 0m)
            {
                throw new ArgumentException("A weight can't be negative.", nameof(weights));
            }

            if (weights[i] > 0m)
            {
                sum += weights[i];
                last = i;
            }
        }

        var totalMinor = Math.Abs(ToMinorUnits(total, currency));

        if (totalMinor == 0)
        {
            return shares;
        }

        if (last < 0)
        {
            throw new ArgumentException("There is an amount to split and no share has a weight.", nameof(weights));
        }

        var unit = 1m;

        for (var i = CurrencyScale.GetDecimalPlaces(currency); i > 0; i--)
        {
            unit /= 10m;
        }

        if (total < 0m)
        {
            unit = -unit;
        }

        var remaining = totalMinor;

        for (var i = 0; i < last; i++)
        {
            if (weights[i] == 0m)
            {
                continue;
            }

            var share = Math.Min(remaining, (long)Math.Round(totalMinor * (weights[i] / sum), 0, MidpointRounding.AwayFromZero));

            shares[i] = share * unit;
            remaining -= share;
        }

        shares[last] = remaining * unit;

        return shares;
    }
}
