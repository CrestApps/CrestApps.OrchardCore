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

    /// <summary>
    /// Gets or sets how many minutes stock stays held for an order that hasn't been paid for. A payment that arrives
    /// later still sells the units, below zero if they were sold to someone else meanwhile.
    /// </summary>
    public int HoldMinutes { get; set; } = 60;
}
