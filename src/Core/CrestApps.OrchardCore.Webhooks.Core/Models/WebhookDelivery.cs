using CrestApps.Core.Models;
using YesSql;

namespace CrestApps.OrchardCore.Webhooks.Core.Models;

/// <summary>
/// One event sent to one subscription, with every attempt to deliver it: the delivery outbox.
/// </summary>
/// <remarks>
/// Deliveries were first stored by Commerce Webhooks, so the record keeps that stored type name.
/// </remarks>
[SimplifiedTypeName(StoredTypeName)]
public sealed class WebhookDelivery : CatalogItem
{
    /// <summary>
    /// The type name deliveries are stored under. It never changes.
    /// </summary>
    public const string StoredTypeName = "CrestApps.OrchardCore.Commerce.Webhooks.Models.WebhookDelivery, CrestApps.OrchardCore.Commerce.Webhooks";

    /// <summary>
    /// Gets or sets the YesSql document id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the subscription it is sent to.
    /// </summary>
    public string SubscriptionId { get; set; }

    /// <summary>
    /// Gets or sets the event's id, the same for every subscription it was sent to, so a receiver can drop repeats.
    /// </summary>
    public string EventId { get; set; }

    /// <summary>
    /// Gets or sets the event's name.
    /// </summary>
    public string EventName { get; set; }

    /// <summary>
    /// Gets or sets the JSON posted, the same on every attempt.
    /// </summary>
    public string Payload { get; set; }

    /// <summary>
    /// Gets or sets where the delivery stands.
    /// </summary>
    public WebhookDeliveryStatus Status { get; set; }

    /// <summary>
    /// Gets or sets how many attempts were made.
    /// </summary>
    public int Attempts { get; set; }

    /// <summary>
    /// Gets or sets when it is tried next, while it is pending.
    /// </summary>
    public DateTime? NextAttemptUtc { get; set; }

    /// <summary>
    /// Gets or sets the status the receiver last answered.
    /// </summary>
    public int? LastStatusCode { get; set; }

    /// <summary>
    /// Gets or sets why the last attempt failed: the receiver's answer, cut short, or the error reaching it.
    /// </summary>
    public string LastError { get; set; }

    /// <summary>
    /// Gets or sets when the event was queued.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the last attempt was made.
    /// </summary>
    public DateTime? LastAttemptUtc { get; set; }

    /// <summary>
    /// Gets or sets when the receiver accepted it.
    /// </summary>
    public DateTime? DeliveredUtc { get; set; }
}
