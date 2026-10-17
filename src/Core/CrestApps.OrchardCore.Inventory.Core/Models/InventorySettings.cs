namespace CrestApps.OrchardCore.Inventory.Core.Models;

/// <summary>
/// Settings › Commerce › Inventory.
/// </summary>
public sealed class InventorySettings
{
    /// <summary>
    /// Gets or sets how few available units count as low stock for products without their own threshold.
    /// </summary>
    public int DefaultLowStockThreshold { get; set; } = 5;
}
