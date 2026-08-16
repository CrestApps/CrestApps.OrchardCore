using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Subscriptions.Core.Indexes;

/// <summary>
/// Indexes payment transactions recorded for completed subscription sessions.
/// </summary>
public sealed class SubscriptionTransactionIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the amount captured for the transaction.
    /// </summary>
    public double Amount { get; set; }

    /// <summary>
    /// Gets or sets the tax portion of the transaction amount.
    /// </summary>
    public double TaxAmount { get; set; }

    /// <summary>
    /// Gets or sets the UTC time associated with the transaction record.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the payment gateway that processed the transaction.
    /// </summary>
    public string GatewayId { get; set; }

    /// <summary>
    /// Gets or sets the environment mode used by the payment gateway for the transaction.
    /// </summary>
    public GatewayMode GatewayMode { get; set; }

    /// <summary>
    /// Gets or sets the payment gateway transaction identifier.
    /// </summary>
    public string GatewayTransactionId { get; set; }

    /// <summary>
    /// Gets or sets the status of the recorded payment transaction.
    /// </summary>
    public PaymentStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the content type of the subscription content item associated with the transaction.
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the subscription content item associated with the transaction.
    /// </summary>
    public string ContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the version identifier of the subscription content item associated with the transaction.
    /// </summary>
    public string ContentItemVersionId { get; set; }

    /// <summary>
    /// Gets or sets the subscription session identifier that recorded the transaction.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user that owns the subscription session.
    /// </summary>
    public string OwnerId { get; set; }
}
