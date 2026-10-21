using System.Text.Json.Serialization;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Inventory.Core.Models;

/// <summary>
/// Stock ordered from a supplier, to be received at a location. Receiving its lines adds their units to stock there.
/// </summary>
public sealed class PurchaseOrder : CatalogItem
{
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the number staff and the supplier know the order by, such as <c>PO-261019-K3QZ</c>.
    /// </summary>
    public string Number { get; set; }

    public string Supplier { get; set; }

    public string SupplierEmail { get; set; }

    /// <summary>
    /// Gets or sets the location the stock is received at.
    /// </summary>
    public string LocationId { get; set; } = InventoryConstants.DefaultLocationId;

    public string Currency { get; set; }

    public PurchaseOrderStatus Status { get; set; }

    /// <summary>
    /// Gets or sets when the stock is expected, or <see langword="null"/>.
    /// </summary>
    public DateTime? ExpectedUtc { get; set; }

    public string Note { get; set; }

    public List<PurchaseOrderLine> Lines { get; set; } = [];

    public DateTime CreatedUtc { get; set; }

    public DateTime ModifiedUtc { get; set; }

    public DateTime? OrderedUtc { get; set; }

    public DateTime? ReceivedUtc { get; set; }

    public string OwnerId { get; set; }

    public string Author { get; set; }

    [JsonIgnore]
    public int Ordered
        => Lines.Sum(line => line.Ordered);

    [JsonIgnore]
    public int Received
        => Lines.Sum(line => line.Received);

    [JsonIgnore]
    public decimal Total
        => Lines.Sum(line => line.UnitCost * line.Ordered);

    /// <summary>
    /// Gets whether its lines can still be changed: until it's sent to the supplier.
    /// </summary>
    [JsonIgnore]
    public bool IsDraft
        => Status == PurchaseOrderStatus.Draft;

    /// <summary>
    /// Gets whether stock can be received for it: once ordered, until all of it is in or it's canceled.
    /// </summary>
    [JsonIgnore]
    public bool CanReceive
        => Status is PurchaseOrderStatus.Ordered or PurchaseOrderStatus.PartiallyReceived;
}

/// <summary>
/// Units of one stock item ordered, and how many have arrived.
/// </summary>
public sealed class PurchaseOrderLine
{
    public string LineId { get; set; }

    public string StockItemId { get; set; }

    public string Title { get; set; }

    public string Sku { get; set; }

    public int Ordered { get; set; }

    public int Received { get; set; }

    /// <summary>
    /// Gets or sets what one unit costs from the supplier, in the order's currency.
    /// </summary>
    public decimal UnitCost { get; set; }

    [JsonIgnore]
    public int Outstanding
        => Math.Max(0, Ordered - Received);
}

public enum PurchaseOrderStatus
{
    /// <summary>
    /// Being put together; not sent to the supplier yet.
    /// </summary>
    Draft = 0,

    Ordered = 1,

    PartiallyReceived = 2,

    Received = 3,

    Canceled = 4,
}
