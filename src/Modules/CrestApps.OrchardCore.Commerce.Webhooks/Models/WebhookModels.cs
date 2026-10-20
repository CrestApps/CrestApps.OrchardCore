using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Commerce.Webhooks.Models;

/// <summary>
/// A URL the store posts events to: which events, and the secret it signs them with.
/// </summary>
public sealed class WebhookSubscription : CatalogItem
{
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets what the merchant calls it, such as the system it feeds.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the URL events are posted to.
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// Gets or sets the secret payloads are signed with, protected at rest.
    /// </summary>
    public string ProtectedSecret { get; set; }

    /// <summary>
    /// Gets or sets the events sent, by name; <c>*</c> sends every event.
    /// </summary>
    public List<string> Events { get; set; } = [];

    public bool IsEnabled { get; set; } = true;

    public DateTime CreatedUtc { get; set; }

    public DateTime ModifiedUtc { get; set; }

    /// <summary>
    /// Returns whether the subscription takes the event.
    /// </summary>
    public bool Takes(string eventName)
        => IsEnabled && (Events ?? []).Any(entry => entry == WebhookConstants.AllEvents || string.Equals(entry, eventName, StringComparison.Ordinal));
}

/// <summary>
/// One event sent to one subscription, with every attempt to deliver it.
/// </summary>
public sealed class WebhookDelivery : CatalogItem
{
    public long Id { get; set; }

    public string SubscriptionId { get; set; }

    /// <summary>
    /// Gets or sets the event's id, the same for every subscription it was sent to, so a receiver can drop repeats.
    /// </summary>
    public string EventId { get; set; }

    public string EventName { get; set; }

    /// <summary>
    /// Gets or sets the JSON posted, the same on every attempt.
    /// </summary>
    public string Payload { get; set; }

    public WebhookDeliveryStatus Status { get; set; }

    public int Attempts { get; set; }

    /// <summary>
    /// Gets or sets when it's tried next, while it's pending.
    /// </summary>
    public DateTime? NextAttemptUtc { get; set; }

    public int? LastStatusCode { get; set; }

    /// <summary>
    /// Gets or sets why the last attempt failed: the receiver's answer, cut short, or the error reaching it.
    /// </summary>
    public string LastError { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime? LastAttemptUtc { get; set; }

    public DateTime? DeliveredUtc { get; set; }
}

public enum WebhookDeliveryStatus
{
    /// <summary>
    /// Waiting to be sent, or to be sent again.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// The receiver answered with a 2xx status.
    /// </summary>
    Delivered = 1,

    /// <summary>
    /// Every attempt failed; it's not tried again unless sent again by hand.
    /// </summary>
    Failed = 2,
}
