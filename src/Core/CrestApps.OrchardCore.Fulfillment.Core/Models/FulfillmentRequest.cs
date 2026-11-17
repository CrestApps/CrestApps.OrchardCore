using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Fulfillment.Core.Models;

/// <summary>
/// One order sent, or to be sent, to one fulfillment partner.
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
    /// Gets or sets the partner's identifier of the order, once it accepted it.
    /// </summary>
    public string ExternalOrderId { get; set; }

    /// <summary>
    /// Gets or sets what happened last, in words for the store owner.
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
