using CrestApps.OrchardCore.Markets.Models;
using CrestApps.OrchardCore.Payments;

namespace CrestApps.OrchardCore.Markets.Services;

/// <summary>
/// The arithmetic of a market's prices: converting by exchange rates, and rounding by the market's rule.
/// </summary>
public static class MarketPricing
{
    /// <summary>
    /// Converts an amount between two currencies, each given by how much of it one unit of the store's currency is
    /// worth.
    /// </summary>
    public static decimal Convert(decimal amount, decimal fromRate, decimal toRate)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fromRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(toRate);

        return amount / fromRate * toRate;
    }

    /// <summary>
    /// Rounds a converted price by a rounding rule. Prices round up to the rule's next price; a currency without minor
    /// units, such as the yen, rounds up to a whole unit for the .99 and .95 rules.
    /// </summary>
    public static decimal Round(decimal amount, string currency, MarketPriceRounding rounding)
    {
        if (amount <= 0m)
        {
            return CurrencyScale.Round(amount, currency);
        }

        var decimals = CurrencyScale.GetDecimalPlaces(currency);

        switch (rounding)
        {
            case MarketPriceRounding.WholeUnits:
                return decimal.Ceiling(CurrencyScale.Round(amount, currency));

            case MarketPriceRounding.EndIn99:
            case MarketPriceRounding.EndIn95:
                if (decimals == 0)
                {
                    return decimal.Ceiling(amount);
                }

                var ending = rounding == MarketPriceRounding.EndIn99 ? 0.99m : 0.95m;
                var rounded = CurrencyScale.Round(amount, currency);
                var candidate = decimal.Floor(rounded) + ending;

                return candidate >= rounded ? candidate : candidate + 1m;

            default:
                return CurrencyScale.Round(amount, currency);
        }
    }
}
