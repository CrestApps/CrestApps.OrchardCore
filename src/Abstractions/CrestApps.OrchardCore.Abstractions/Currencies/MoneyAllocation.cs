namespace CrestApps.OrchardCore.Currencies;

/// <summary>
/// Spreads an amount of money over weights in whole units of a currency, so the shares always add up to the amount:
/// a document discount over its lines, or a tax worked out on summed amounts back over the items it came from.
/// </summary>
public static class MoneyAllocation
{
    /// <summary>
    /// Spreads <paramref name="total"/> over <paramref name="weights"/> in proportion, in whole units of the currency,
    /// giving the units left by rounding down to the largest remainders first (the earlier one on a tie). A negative
    /// weight beside positive ones (a deduction line) takes a negative share, and the shares still add up.
    /// </summary>
    /// <param name="total">The amount to spread, already rounded to the currency.</param>
    /// <param name="weights">The weights, one per share.</param>
    /// <param name="currency">The ISO-4217 currency code, for its unit.</param>
    /// <returns>The shares, in the order of the weights.</returns>
    public static decimal[] Allocate(decimal total, IReadOnlyList<decimal> weights, string currency)
    {
        ArgumentNullException.ThrowIfNull(weights);

        var shares = new decimal[weights.Count];
        var weightSum = weights.Sum();

        if (weights.Count == 0 || total == 0m)
        {
            return shares;
        }

        if (weightSum == 0m)
        {
            shares[0] = total;

            return shares;
        }

        // Each exact share is rounded down to a unit, which leaves a remainder in [0, unit) whatever the signs of the total
        // and the weights; the remainders add up to a whole number of units, given back one each, largest first.
        var unit = 1m / (decimal)Math.Pow(10, CurrencyScale.GetDecimalPlaces(currency));
        var remainders = new (decimal Remainder, int Index)[weights.Count];
        var allocated = 0m;

        for (var i = 0; i < weights.Count; i++)
        {
            var exact = total * weights[i] / weightSum;
            var floored = Math.Floor(exact / unit) * unit;

            shares[i] = floored;
            allocated += floored;
            remainders[i] = (exact - floored, i);
        }

        var left = (int)Math.Round((total - allocated) / unit);

        foreach (var (_, index) in remainders.OrderByDescending(entry => entry.Remainder).ThenBy(entry => entry.Index).Take(left))
        {
            shares[index] += unit;
        }

        return shares;
    }
}
