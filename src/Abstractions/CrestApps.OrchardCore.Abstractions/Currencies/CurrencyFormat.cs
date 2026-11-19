using System.Collections.Concurrent;
using System.Globalization;

namespace CrestApps.OrchardCore.Currencies;

/// <summary>
/// Writes an amount of money for a person to read, in the two ways the solution shows money: with the currency's code
/// (<c>EUR 20.00</c>), which reads the same everywhere, and with the currency's symbol in a culture's own number format
/// (<c>€20.00</c>, <c>20,00 €</c>). Both use the precision of the currency from <see cref="CurrencyScale"/>.
/// </summary>
public static class CurrencyFormat
{
    private static readonly ConcurrentDictionary<string, string> _symbols = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Writes an amount after the code of its currency, for example <c>EUR 20.00</c>, with the invariant number format.
    /// Use it in text that is stored or sent to people in several cultures, such as a timeline entry or a notice.
    /// </summary>
    /// <param name="amount">The amount, in major units.</param>
    /// <param name="currency">The ISO-4217 code of the currency. Without one, the amount is written alone, never in the
    /// server's currency.</param>
    /// <returns>The amount.</returns>
    public static string WithCode(decimal amount, string currency)
        => string.IsNullOrEmpty(currency)
            ? CurrencyScale.Format(amount, currency)
            : $"{currency} {CurrencyScale.Format(amount, currency)}";

    /// <summary>
    /// Writes an amount with the symbol of its currency, in the number format of a culture: its digit grouping, its
    /// decimal separator and the place it puts the symbol.
    /// </summary>
    /// <param name="amount">The amount, in major units.</param>
    /// <param name="currency">The ISO-4217 code of the currency, or <see langword="null"/> for the currency of the
    /// culture.</param>
    /// <param name="culture">The culture whose number format is used, or <see langword="null"/> for the current
    /// culture.</param>
    /// <param name="trimWholeAmounts">Whether a whole amount is written without decimals, as a price tag is.</param>
    /// <returns>The amount.</returns>
    public static string WithSymbol(decimal amount, string currency, CultureInfo culture = null, bool trimWholeAmounts = false)
    {
        var format = (NumberFormatInfo)(culture ?? CultureInfo.CurrentCulture).NumberFormat.Clone();

        if (!string.IsNullOrEmpty(currency))
        {
            format.CurrencySymbol = GetSymbol(currency);
            format.CurrencyDecimalDigits = CurrencyScale.GetDecimalPlaces(currency);
        }

        if (trimWholeAmounts && decimal.Truncate(amount) == amount)
        {
            format.CurrencyDecimalDigits = 0;
        }

        return amount.ToString("C", format);
    }

    /// <summary>
    /// Gets the symbol of a currency, such as <c>$</c> for <c>USD</c>. It comes from a culture that uses the currency, so
    /// it does not depend on the server's culture. A currency no culture uses is written as its code and a space.
    /// </summary>
    /// <param name="currency">The ISO-4217 code of the currency, or <see langword="null"/> for the currency of the
    /// current culture.</param>
    /// <returns>The symbol.</returns>
    public static string GetSymbol(string currency)
        => string.IsNullOrEmpty(currency)
            ? CultureInfo.CurrentCulture.NumberFormat.CurrencySymbol
            : _symbols.GetOrAdd(currency, FindSymbol);

    private static string FindSymbol(string currency)
    {
        foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        {
            try
            {
                var region = new RegionInfo(culture.Name);

                if (string.Equals(region.ISOCurrencySymbol, currency, StringComparison.OrdinalIgnoreCase))
                {
                    return region.CurrencySymbol;
                }
            }
            catch (ArgumentException)
            {
                // Some cultures have no region.
            }
        }

        return currency.ToUpperInvariant() + " ";
    }
}
