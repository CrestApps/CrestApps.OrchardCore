using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Inventory.Core.Models;

/// <summary>
/// How a product's stock is kept: whether it is counted at all, whether it may be sold when none is left, and when it
/// counts as low. The counts themselves are stock items, one per variant, not part of the content item.
/// </summary>
public sealed class InventoryPart : ContentPart
{
    /// <summary>
    /// Gets or sets whether the product's stock is counted. A product that isn't counted can always be bought.
    /// </summary>
    public bool TrackInventory { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the product may be sold when none is left, as a backorder.
    /// </summary>
    public bool AllowBackorder { get; set; }

    /// <summary>
    /// Gets or sets how few available units count as low stock, or <see langword="null"/> for the site's default.
    /// </summary>
    public int? LowStockThreshold { get; set; }

    /// <summary>
    /// Gets or sets when a product not out yet ships. With backorders allowed, buyers pre-order it until then while
    /// none are in stock.
    /// </summary>
    public DateTime? ReleaseDateUtc { get; set; }
}
