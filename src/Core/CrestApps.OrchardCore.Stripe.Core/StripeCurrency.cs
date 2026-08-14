using System.Globalization;

namespace CrestApps.OrchardCore.Stripe.Core;

/// <summary>
/// Converts monetary amounts between major units (e.g. dollars) and the integer minor units that
/// Stripe settles in, honoring each currency's number of decimal places.
///
/// Stripe does not use a universal hundredths conversion. Zero-decimal currencies (such as JPY) are
/// exchanged as whole units, so multiplying by 100 would overcharge the customer 100x. Three-decimal
/// currencies (such as KWD) are exchanged in thousandths, but Stripe additionally requires the smallest
/// unit to be a multiple of 10. This helper centralizes those rules so no gateway call site performs a
/// hardcoded conversion.
/// </summary>
public static class StripeCurrency
{
    // Stripe zero-decimal currencies. Amounts are exchanged as whole units (no multiplication).
    // https://docs.stripe.com/currencies#zero-decimal
    //
    // Note: UGX and ISK are listed by Stripe as zero-decimal but are "special cases" that must be sent
    // as a two-decimal value evenly divisible by 100 (e.g. 5 UGX => amount 500). They are therefore
    // intentionally NOT in this set and fall through to the two-decimal default. HUF and TWD accept
    // two-decimal charge amounts (they are only zero-decimal for payouts), so they are also excluded.
    private static readonly HashSet<string> _zeroDecimalCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "BIF", "CLP", "DJF", "GNF", "JPY", "KMF", "KRW", "MGA", "PYG",
        "RWF", "VND", "VUV", "XAF", "XOF", "XPF",
    };

    // Stripe three-decimal currencies. Amounts are exchanged in thousandths but the smallest currency
    // unit must be a multiple of 10 (Stripe rounds to the nearest ten).
    // https://docs.stripe.com/currencies#three-decimal
    private static readonly HashSet<string> _threeDecimalCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "BHD", "JOD", "KWD", "OMR", "TND",
    };

    /// <summary>
    /// Returns the number of decimal places Stripe uses for the given ISO-4217 currency code.
    /// Unknown currencies default to two decimals, matching the majority of supported currencies.
    /// </summary>
    public static int GetDecimalPlaces(string currency)
    {
        ArgumentException.ThrowIfNullOrEmpty(currency);

        if (_zeroDecimalCurrencies.Contains(currency))
        {
            return 0;
        }

        if (_threeDecimalCurrencies.Contains(currency))
        {
            return 3;
        }

        return 2;
    }

    /// <summary>
    /// Converts an amount expressed in major units to the integer minor units Stripe expects for the currency.
    /// Rounding is performed away from zero at the currency's precision. For three-decimal currencies the
    /// result is additionally rounded to the nearest multiple of 10 as required by Stripe.
    /// </summary>
    public static long ToMinorUnits(decimal amount, string currency)
    {
        var decimals = GetDecimalPlaces(currency);

        if (decimals == 3)
        {
            // Stripe requires three-decimal amounts to be a multiple of 10 in the smallest unit.
            // Round directly at that granularity (0.01 major units) to avoid a double-rounding error.
            var tens = (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);

            return tens * 10;
        }

        var scaled = amount * Pow10(decimals);

        return (long)Math.Round(scaled, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Converts an amount expressed in major units (carried as <see cref="double"/> in legacy models) to
    /// integer minor units for the currency. Prefer the <see cref="decimal"/> overload for new code.
    /// </summary>
    public static long ToMinorUnits(double amount, string currency)
        => ToMinorUnits((decimal)amount, currency);

    /// <summary>
    /// Converts integer minor units received from Stripe back to a major-unit amount for the currency.
    /// </summary>
    public static decimal FromMinorUnits(long minorUnits, string currency)
    {
        var decimals = GetDecimalPlaces(currency);

        return minorUnits / Pow10(decimals);
    }

    /// <summary>
    /// Converts integer minor units received from Stripe to a major-unit amount rounded to the currency's
    /// precision and returned as a <see cref="double"/> for legacy models.
    /// </summary>
    public static double FromMinorUnitsToDouble(long minorUnits, string currency)
        => (double)FromMinorUnits(minorUnits, currency);

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
    /// Formats a major-unit amount using the invariant culture with the currency's decimal precision.
    /// </summary>
    public static string Format(decimal amount, string currency)
        => amount.ToString("F" + GetDecimalPlaces(currency).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
}
