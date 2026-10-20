using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Companies.Models;

/// <summary>
/// Prices for the companies that buy on it: a percentage off every product, and a price for each unit of some, by SKU.
/// A product's own price on the list wins over the percentage; either way the buyer pays the lower of it and the
/// store's price.
/// </summary>
public sealed class PriceList : CatalogItem
{
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the percentage off every product not on the list by SKU, from 0 to 100, or none.
    /// </summary>
    public decimal? PercentOff { get; set; }

    /// <summary>
    /// Gets or sets the currency of the prices by SKU: the store's when the list was saved.
    /// </summary>
    public string Currency { get; set; }

    public List<PriceListEntry> Entries { get; set; } = [];

    public DateTime CreatedUtc { get; set; }

    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Returns the list's price for a SKU, or <see langword="null"/>.
    /// </summary>
    public PriceListEntry Find(string sku)
        => string.IsNullOrWhiteSpace(sku)
            ? null
            : Entries.FirstOrDefault(entry => string.Equals(entry.Sku, sku.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// A product's price for each unit on a price list.
/// </summary>
public sealed class PriceListEntry
{
    public string Sku { get; set; }

    public decimal UnitPrice { get; set; }
}
