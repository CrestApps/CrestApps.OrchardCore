using System.Globalization;

namespace CrestApps.OrchardCore.Payments;

/// <summary>
/// Provides provider-neutral, ISO-4217 aware conversion between major currency units (e.g. dollars) and
/// the integer minor units (e.g. cents) that payment gateways settle in.
///
/// A universal hundredths conversion is unsafe: zero-decimal currencies such as JPY are exchanged as
/// whole units, so multiplying by 100 would overcharge the customer 100x, while three-decimal currencies
/// such as KWD are exchanged in thousandths. Centralizing the currency precision here lets the checkout
/// framework compare and round money correctly for every supported currency without depending on any
/// specific payment provider.
/// </summary>
public static class CurrencyScale
{
    // Zero-decimal currencies are exchanged as whole units (no multiplication).
    private static readonly HashSet<string> _zeroDecimalCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "BIF", "CLP", "DJF", "GNF", "ISK", "JPY", "KMF", "KRW", "PYG",
        "RWF", "UGX", "UYI", "VND", "VUV", "XAF", "XOF", "XPF",
    };

    // Three-decimal currencies are exchanged in thousandths.
    private static readonly HashSet<string> _threeDecimalCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "BHD", "IQD", "JOD", "KWD", "LYD", "OMR", "TND",
    };

    // Four-decimal currencies are exchanged in ten-thousandths.
    private static readonly HashSet<string> _fourDecimalCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "CLF", "UYW",
    };

    /// <summary>
    /// Returns the number of decimal places used for the supplied ISO-4217 currency code. Unknown or
    /// empty currencies default to two decimals, which matches the majority of supported currencies.
    /// </summary>
    /// <param name="currency">The ISO-4217 currency code (for example <c>USD</c>).</param>
    public static int GetDecimalPlaces(string currency)
    {
        if (string.IsNullOrEmpty(currency))
        {
            return 2;
        }

        if (_zeroDecimalCurrencies.Contains(currency))
        {
            return 0;
        }

        if (_threeDecimalCurrencies.Contains(currency))
        {
            return 3;
        }

        if (_fourDecimalCurrencies.Contains(currency))
        {
            return 4;
        }

        return 2;
    }

    /// <summary>
    /// Converts an amount expressed in major units to the integer minor units for the supplied currency.
    /// Rounding is performed away from zero at the currency's precision.
    /// </summary>
    /// <param name="amount">The amount expressed in major units.</param>
    /// <param name="currency">The ISO-4217 currency code used to determine precision.</param>
    public static long ToMinorUnits(decimal amount, string currency)
    {
        var decimals = GetDecimalPlaces(currency);
        var scaled = amount * Pow10(decimals);

        return (long)Math.Round(scaled, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Rounds an amount to the precision of the supplied currency using away-from-zero rounding.
    /// </summary>
    /// <param name="amount">The amount expressed in major units.</param>
    /// <param name="currency">The ISO-4217 currency code used to determine precision.</param>
    public static decimal Round(decimal amount, string currency)
        => Math.Round(amount, GetDecimalPlaces(currency), MidpointRounding.AwayFromZero);

    /// <summary>
    /// Formats a major-unit amount using the invariant culture at the currency's precision.
    /// </summary>
    /// <param name="amount">The amount expressed in major units.</param>
    /// <param name="currency">The ISO-4217 currency code used to determine precision.</param>
    public static string Format(decimal amount, string currency)
        => amount.ToString("F" + GetDecimalPlaces(currency).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    private static decimal Pow10(int exponent)
        => exponent switch
        {
            0 => 1m,
            1 => 10m,
            2 => 100m,
            3 => 1000m,
            _ => (decimal)Math.Pow(10, exponent),
        };
}
