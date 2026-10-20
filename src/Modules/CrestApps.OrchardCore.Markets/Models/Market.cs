using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Markets.Models;

/// <summary>
/// The countries a store sells to on the same terms: the currency they pay in and its exchange rate from the store's
/// currency, how converted prices are rounded, the language they read, and whether prices include tax.
/// </summary>
public sealed class Market : CatalogItem
{
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the two-letter codes of the countries in the market.
    /// </summary>
    public List<string> Countries { get; set; } = [];

    /// <summary>
    /// Gets or sets the currency the market pays in.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets how much of <see cref="Currency"/> one unit of the store's currency is worth. Prices in the store's
    /// currency are multiplied by it; a product's own price in the market's currency is used as it is.
    /// </summary>
    public decimal ExchangeRate { get; set; } = 1m;

    public MarketPriceRounding Rounding { get; set; }

    /// <summary>
    /// Gets or sets the culture the market's shoppers read the storefront in, such as de-DE, or none to keep theirs.
    /// </summary>
    public string Culture { get; set; }

    /// <summary>
    /// Gets or sets whether the market's prices include tax: the shopper pays the price shown, and the tax is taken out
    /// of it.
    /// </summary>
    public bool PricesIncludeTax { get; set; }

    public bool IsEnabled { get; set; } = true;

    public DateTime CreatedUtc { get; set; }

    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Returns whether a country is in the market.
    /// </summary>
    public bool Covers(string country)
        => !string.IsNullOrEmpty(country) && Countries.Contains(country, StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// How a price converted into a market's currency is rounded. Prices round up, so a converted price is never below
/// what the rate gives.
/// </summary>
public enum MarketPriceRounding
{
    /// <summary>
    /// To the currency's smallest unit, such as the cent.
    /// </summary>
    None,

    /// <summary>
    /// Up to a whole unit, such as 24.00.
    /// </summary>
    WholeUnits,

    /// <summary>
    /// Up to the next price ending in .99, such as 23.99.
    /// </summary>
    EndIn99,

    /// <summary>
    /// Up to the next price ending in .95, such as 23.95.
    /// </summary>
    EndIn95,
}
