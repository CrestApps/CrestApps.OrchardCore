using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Inventory.Core.Models;

/// <summary>
/// Units of a stock item held for an order while it is paid for: held until the order is paid (sold), canceled or
/// the hold expires (released).
/// </summary>
public sealed class StockReservation : CatalogItem
{
    public long Id { get; set; }

    public string StockItemId { get; set; }

    public string OrderId { get; set; }

    public int Quantity { get; set; }

    public StockReservationStatus Status { get; set; }

    /// <summary>
    /// Gets or sets when an unpaid hold lets the units go.
    /// </summary>
    public DateTime ExpiresUtc { get; set; }

    /// <summary>
    /// Gets or sets why the hold ended without a sale, such as the order being canceled or the hold expiring.
    /// </summary>
    public string ReleaseReason { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime ModifiedUtc { get; set; }
}

public enum StockReservationStatus
{
    /// <summary>
    /// The units are held for the order.
    /// </summary>
    Held = 0,

    /// <summary>
    /// The order was paid and the units sold.
    /// </summary>
    Committed = 1,

    /// <summary>
    /// The hold ended without a sale.
    /// </summary>
    Released = 2,
}
