using System.Globalization;
using CrestApps.OrchardCore.Payments;

namespace CrestApps.OrchardCore.Stripe.Core;

/// <summary>
/// Converts monetary amounts between major units (e.g. dollars) and the integer minor units that
/// Stripe settles in.
///
/// A currency's precision comes from <see cref="CurrencyScale"/> (ISO 4217), the one precision table that
/// tax, payments and display share. This class only adds what Stripe's API does differently from ISO 4217,
/// as explicit overrides: the exponent Stripe's <c>amount</c> field uses for a few currencies, and the rule
/// that three-decimal amounts are a multiple of 10. No gateway call site performs its own conversion.
/// </summary>
public static class StripeCurrency
{
    // The exponent of Stripe's amount field where it differs from ISO 4217.
    // https://docs.stripe.com/currencies#special-cases and #zero-decimal
    private static readonly Dictionary<string, int> _amountExponentOverrides = new(StringComparer.OrdinalIgnoreCase)
    {
        // ISO 4217 gives ISK and UGX no decimals. Stripe still sends them as two-decimal amounts, which must
        // be evenly divisible by 100 (5 UGX is amount 500), for backwards compatibility.
        ["ISK"] = 2,
        ["UGX"] = 2,

        // ISO 4217 gives MGA two decimals (the iraimbilanja), but Stripe charges it as a zero-decimal currency.
        ["MGA"] = 0,
    };

    // IQD and LYD follow ISO 4217 (three decimals) like every other currency: Stripe does not list them as
    // three-decimal currencies, and if it settles them at all its three-decimal rule below applies.

    /// <summary>
    /// Returns the number of decimal places of Stripe's <c>amount</c> field for the given ISO-4217 currency code:
    /// the currency's precision from <see cref="CurrencyScale"/>, except where Stripe's API differs.
    /// </summary>
    /// <param name="currency">The ISO-4217 currency code.</param>
    public static int GetDecimalPlaces(string currency)
    {
        ArgumentException.ThrowIfNullOrEmpty(currency);

        return _amountExponentOverrides.TryGetValue(currency, out var exponent)
            ? exponent
            : CurrencyScale.GetDecimalPlaces(currency);
    }

    /// <summary>
    /// Converts an amount expressed in major units to the integer minor units Stripe expects for the currency.
    /// The amount is first rounded away from zero to the finest precision both the currency and Stripe allow: whole
    /// units for ISK, UGX and MGA, and hundredths for three-decimal currencies, whose Stripe amount must be a multiple
    /// of 10.
    /// </summary>
    /// <param name="amount">The amount expressed in major units.</param>
    /// <param name="currency">The ISO-4217 currency code.</param>
    public static long ToMinorUnits(decimal amount, string currency)
    {
        var exponent = GetDecimalPlaces(currency);
        var precision = Math.Min(exponent, CurrencyScale.GetDecimalPlaces(currency));

        if (exponent == 3)
        {
            // Stripe requires three-decimal amounts to be a multiple of 10 in the smallest unit. Rounding straight to
            // hundredths avoids a double-rounding error.
            precision = 2;
        }

        var rounded = Math.Round(amount, precision, MidpointRounding.AwayFromZero);

        return (long)(rounded * Pow10(exponent));
    }

    /// <summary>
    /// Converts integer minor units received from Stripe back to a major-unit amount for the currency.
    /// </summary>
    /// <param name="minorUnits">The amount in Stripe's minor units.</param>
    /// <param name="currency">The ISO-4217 currency code.</param>
    public static decimal FromMinorUnits(long minorUnits, string currency)
    {
        var decimals = GetDecimalPlaces(currency);

        return minorUnits / Pow10(decimals);
    }

    private static decimal Pow10(int exponent)
        => exponent switch
        {
            0 => 1m,
            1 => 10m,
            2 => 100m,
            3 => 1000m,
            _ => (decimal)Math.Pow(10, exponent),
        };

    /// <summary>
    /// Formats a major-unit amount using the invariant culture with the precision of Stripe's amount field.
    /// </summary>
    /// <param name="amount">The amount expressed in major units.</param>
    /// <param name="currency">The ISO-4217 currency code.</param>
    public static string Format(decimal amount, string currency)
        => amount.ToString("F" + GetDecimalPlaces(currency).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
}
