using System.Text.Json.Serialization;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Inventory.Core.Models;

/// <summary>
/// How many units of one product, or one of its variants, a location holds. The counts are a cache of the stock
/// movements, which can rebuild them.
/// </summary>
public sealed class StockItem : CatalogItem
{
    public long Id { get; set; }

    public string ContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the variant counted, or <see langword="null"/> for a product without variants.
    /// </summary>
    public string VariantId { get; set; }

    public string LocationId { get; set; } = InventoryConstants.DefaultLocationId;

    /// <summary>
    /// Gets or sets the SKU, copied from the product or variant, for search, import and export.
    /// </summary>
    public string Sku { get; set; }

    /// <summary>
    /// Gets or sets the product's title, with the variant's after it, as the lists show it.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the units the location holds.
    /// </summary>
    public int OnHand { get; set; }

    /// <summary>
    /// Gets or sets the units held for orders that are being paid for.
    /// </summary>
    public int Reserved { get; set; }

    /// <summary>
    /// Gets the units that can still be sold.
    /// </summary>
    [JsonIgnore]
    public int Available => OnHand - Reserved;

    /// <summary>
    /// Gets or sets how few available units count as low stock: the product's own threshold, or the site's default.
    /// </summary>
    public int LowStockThreshold { get; set; }

    /// <summary>
    /// Gets or sets whether the product may be sold when none is left.
    /// </summary>
    public bool AllowBackorder { get; set; }

    /// <summary>
    /// Gets or sets whether the product or variant counted is gone or no longer tracked. Its history is kept.
    /// </summary>
    public bool Archived { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime ModifiedUtc { get; set; }

    [JsonIgnore]
    public bool IsLow => Available <= LowStockThreshold;
}
