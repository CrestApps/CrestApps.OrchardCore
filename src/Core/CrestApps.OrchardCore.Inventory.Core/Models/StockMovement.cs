using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Inventory.Core.Models;

/// <summary>
/// One change to a stock item's units on hand, kept for ever: what changed, by how much, why and by whom.
/// </summary>
public sealed class StockMovement : CatalogItem
{
    public long Id { get; set; }

    public string StockItemId { get; set; }

    public StockMovementKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the change in units on hand: positive when stock came in, negative when it went out.
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Gets or sets the units on hand after the change.
    /// </summary>
    public int OnHandAfter { get; set; }

    /// <summary>
    /// Gets or sets the location the units came in or went out at, or <see langword="null"/> for the default location.
    /// </summary>
    public string LocationId { get; set; }

    /// <summary>
    /// Gets or sets the purchase order the units were received for, or <see langword="null"/>.
    /// </summary>
    public string PurchaseOrderId { get; set; }

    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the order that sold or returned the units, if any.
    /// </summary>
    public string OrderId { get; set; }

    /// <summary>
    /// Gets or sets the kind of record the movement is for, such as <c>WorkOrder</c>, beside orders.
    /// </summary>
    public string ReferenceType { get; set; }

    /// <summary>
    /// Gets or sets the record the movement is for.
    /// </summary>
    public string ReferenceId { get; set; }

    public string ActorId { get; set; }

    public string ActorName { get; set; }

    public DateTime Utc { get; set; }
}

public enum StockMovementKind
{
    /// <summary>
    /// Units arrived, such as a delivery from a supplier.
    /// </summary>
    Received = 0,

    /// <summary>
    /// A correction up or down, such as damaged or found units.
    /// </summary>
    Adjusted = 1,

    /// <summary>
    /// The units were counted and the count replaced what was recorded.
    /// </summary>
    Counted = 2,

    /// <summary>
    /// A paid order took the units.
    /// </summary>
    Sold = 3,

    /// <summary>
    /// Units sold came back.
    /// </summary>
    Returned = 4,

    /// <summary>
    /// Units moved from one location to another: one movement out of the first and one into the second.
    /// </summary>
    Transferred = 5,

    /// <summary>
    /// Used up, such as parts fitted on a work order.
    /// </summary>
    Consumed = 6,
}
