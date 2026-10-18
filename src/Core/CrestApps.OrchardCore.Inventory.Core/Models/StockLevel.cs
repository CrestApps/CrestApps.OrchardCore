namespace CrestApps.OrchardCore.Inventory.Core.Models;

/// <summary>
/// How much of a product is left, as stores talk about it.
/// </summary>
public enum StockLevel
{
    /// <summary>
    /// More than the low-stock threshold is available.
    /// </summary>
    InStock = 0,

    /// <summary>
    /// Some is available, but no more than the low-stock threshold.
    /// </summary>
    Low = 1,

    /// <summary>
    /// None is available.
    /// </summary>
    Out = 2,
}

public static class StockLevels
{
    /// <summary>
    /// Gets the level that <paramref name="available"/> units stand at for a product with this threshold.
    /// </summary>
    public static StockLevel Of(int available, int lowStockThreshold)
        => available <= 0
        ? StockLevel.Out
        : available <= lowStockThreshold
        ? StockLevel.Low
        : StockLevel.InStock;
}
