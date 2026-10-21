using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Shipping.Models;

/// <summary>
/// A parcel of an order: some or all of its items, sent with a carrier and followed by a tracking number. An order can
/// be sent in several shipments.
/// </summary>
public sealed class Shipment : CatalogItem
{
    public long Id { get; set; }

    public string OrderId { get; set; }

    public string OrderNumber { get; set; }

    /// <summary>
    /// Gets or sets the shipment's place among the order's shipments, from 1.
    /// </summary>
    public int Number { get; set; }

    public List<ShipmentLine> Lines { get; set; } = [];

    public ShipmentStatus Status { get; set; }

    public string Carrier { get; set; }

    public string TrackingNumber { get; set; }

    public string TrackingUrl { get; set; }

    /// <summary>
    /// Gets or sets a note for the store, never shown to the buyer.
    /// </summary>
    public string Note { get; set; }

    /// <summary>
    /// Gets or sets where the label bought for the shipment is printed from, or <see langword="null"/>.
    /// </summary>
    public string LabelUrl { get; set; }

    /// <summary>
    /// Gets or sets what the label cost.
    /// </summary>
    public decimal? LabelCost { get; set; }

    public string LabelCurrency { get; set; }

    /// <summary>
    /// Gets or sets the carrier's latest word on the parcel, such as "In transit", or <see langword="null"/>.
    /// </summary>
    public string TrackingStatus { get; set; }

    /// <summary>
    /// Gets or sets where the parcel has been, newest first, as the carrier reported it.
    /// </summary>
    public List<ShipmentTrackingEvent> TrackingEvents { get; set; } = [];

    public DateTime CreatedUtc { get; set; }

    public DateTime ModifiedUtc { get; set; }

    public DateTime? ShippedUtc { get; set; }

    public DateTime? DeliveredUtc { get; set; }

    public DateTime? ReturnedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the buyer was emailed that the shipment left, so they are told once.
    /// </summary>
    public DateTime? ShippedEmailSentUtc { get; set; }

    public int ItemCount
        => Lines?.Sum(line => line.Quantity) ?? 0;
}

/// <summary>
/// Units of one line of the order in a shipment.
/// </summary>
/// <summary>
/// Something the carrier reported about the parcel.
/// </summary>
public sealed class ShipmentTrackingEvent
{
    public DateTime Utc { get; set; }

    public string Status { get; set; }

    public string Message { get; set; }

    public string Location { get; set; }
}

public sealed class ShipmentLine
{
    /// <summary>
    /// Gets or sets the id of the order line.
    /// </summary>
    public string LineId { get; set; }

    public string Title { get; set; }

    public string Sku { get; set; }

    public int Quantity { get; set; }
}

public enum ShipmentStatus
{
    /// <summary>
    /// Being packed: its items are set aside, but it hasn't left.
    /// </summary>
    Pending = 0,

    Shipped = 1,

    Delivered = 2,

    /// <summary>
    /// Came back. Its items count as not shipped, so they can be sent again.
    /// </summary>
    Returned = 3,
}
