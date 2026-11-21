using CrestApps.OrchardCore.Inventory.Core.Models;

namespace CrestApps.OrchardCore.Inventory.Models;

/// <summary>
/// A row of the stock history list of a stock item: a change of its units, with the name of the location it happened at.
/// </summary>
public sealed class StockMovementRow
{
    /// <summary>
    /// Gets or sets the change.
    /// </summary>
    public StockMovement Movement { get; set; }

    /// <summary>
    /// Gets or sets the name of the location the change happened at, or <see langword="null"/> when the store counts
    /// its stock in one place.
    /// </summary>
    public string LocationName { get; set; }
}
