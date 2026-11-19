using CrestApps.OrchardCore.Currencies;

namespace CrestApps.OrchardCore.Products.Core.Services;

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
    /// <summary>
    /// Formats an amount in a currency.
    /// </summary>
    /// <param name="amount">The amount, in major units.</param>
    /// <param name="currency">The ISO-4217 currency code, or <see langword="null"/> for the current culture's currency.</param>
    public static string Format(decimal amount, string currency)
        => CurrencyFormat.WithSymbol(amount, currency, culture: null, trimWholeAmounts: true);

    /// <summary>
    /// The symbol of a currency, such as <c>$</c>, or the current culture's when none is given.
    /// </summary>
    /// <param name="currency">The ISO-4217 currency code.</param>
    public static string GetSymbol(string currency)
        => CurrencyFormat.GetSymbol(currency);
}
