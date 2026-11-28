using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Fulfillment.Core.Models;

/// <summary>
/// One order sent, or to be sent, to one fulfillment partner. The units it covers are reserved in a Shipping
/// shipment, so the order's fulfillment status, the buyer's shipped email and the order's Shipments card all follow it.
/// </summary>
public sealed class FulfillmentRequest : CatalogItem
{
    /// <summary>
    /// Gets or sets the identifier of the stored document.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the store's identifier of the order.
    /// </summary>
    public string OrderId { get; set; }

    /// <summary>
    /// Gets or sets the order number shown to people.
    /// </summary>
    public string OrderNumber { get; set; }

    /// <summary>
    /// Gets or sets the technical name of the partner.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets where the order stands with the partner.
    /// </summary>
    public FulfillmentRequestStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the status a request had when a person put it in line again for the background sender, until it is
    /// sent or settled; <see langword="null"/> otherwise. It is judged by that status, as if it had been sent from the
    /// page, so a request that needed attention and is stopped again before the partner is called tells nobody twice.
    /// </summary>
    public FulfillmentRequestStatus? QueuedFromStatus { get; set; }

    /// <summary>
    /// Gets or sets the lines assigned to this partner, as they were sent.
    /// </summary>
    public List<FulfillmentRequestLine> Lines { get; set; } = [];

    /// <summary>
    /// Gets or sets the identifier of the pending Shipping shipment that reserves these lines while the partner has
    /// them, or <see langword="null"/> when none is held.
    /// </summary>
    public string ShipmentId { get; set; }

    /// <summary>
    /// Gets or sets the partner's identifier of the order, once it accepted it.
    /// </summary>
    public string ExternalOrderId { get; set; }

    /// <summary>
    /// Gets or sets why the request is where it is, as one of the <see cref="FulfillmentReasons"/> codes; the order
    /// card and the list page turn it into a localized sentence.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the values the reason's sentence names, such as the order status or the lines.
    /// </summary>
    public List<string> ReasonArguments { get; set; } = [];

    /// <summary>
    /// Gets or sets the partner's own message, as the provider returned it.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets how many times sending was tried.
    /// </summary>
    public int Attempts { get; set; }

    /// <summary>
    /// Gets or sets when the request may be sent next, while it is pending.
    /// </summary>
    public DateTime NextAttemptUtc { get; set; }

    /// <summary>
    /// Gets or sets when the request was recorded.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the request last changed.
    /// </summary>
    public DateTime ModifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the partner accepted the order.
    /// </summary>
    public DateTime? SentUtc { get; set; }
}
