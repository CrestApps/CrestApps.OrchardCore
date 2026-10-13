using System.Collections.Concurrent;
using System.Globalization;

namespace CrestApps.OrchardCore.Products.Services;

/// <summary>
/// Formats a price for a storefront: the currency's own symbol and precision, the visitor's digit grouping,
/// and no ".00" on a whole amount.
/// </summary>
/// <remarks>
/// A car at $15,599 and a filter at $67.46 should both read naturally, so cents are shown only when there are
/// some. The symbol comes from a culture that uses the currency, so a US dollar price reads "$" and a euro
/// price "€" whatever the server's culture is.
/// </remarks>
public static class ProductPriceFormatter
{
    private static readonly ConcurrentDictionary<string, (string Symbol, int Digits)> _currencies = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Formats an amount in a currency.
    /// </summary>
    /// <param name="amount">The amount, in major units.</param>
    /// <param name="currency">The ISO-4217 currency code, or <see langword="null"/> for the current culture's currency.</param>
    public static string Format(decimal amount, string currency)
    {
        var format = (NumberFormatInfo)CultureInfo.CurrentCulture.NumberFormat.Clone();

        if (!string.IsNullOrEmpty(currency))
        {
            var (symbol, digits) = _currencies.GetOrAdd(currency, Describe);

            format.CurrencySymbol = symbol;
            format.CurrencyDecimalDigits = digits;
        }

        if (decimal.Truncate(amount) == amount)
        {
            format.CurrencyDecimalDigits = 0;
        }

        return amount.ToString("C", format);
    }

    /// <summary>
    /// The symbol of a currency, such as <c>$</c>, or the current culture's when none is given.
    /// </summary>
    /// <param name="currency">The ISO-4217 currency code.</param>
    public static string GetSymbol(string currency)
        => string.IsNullOrEmpty(currency)
            ? CultureInfo.CurrentCulture.NumberFormat.CurrencySymbol
            : _currencies.GetOrAdd(currency, Describe).Symbol;

    private static (string Symbol, int Digits) Describe(string currency)
    {
        foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        {
            try
            {
                var region = new RegionInfo(culture.Name);

                if (string.Equals(region.ISOCurrencySymbol, currency, StringComparison.OrdinalIgnoreCase))
                {
                    return (region.CurrencySymbol, culture.NumberFormat.CurrencyDecimalDigits);
                }
            }
            catch (ArgumentException)
            {
                // Some cultures have no region.
            }
        }

        return (currency.ToUpperInvariant() + " ", 2);
    }
}
