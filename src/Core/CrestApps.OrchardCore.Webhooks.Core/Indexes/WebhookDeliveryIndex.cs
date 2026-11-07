using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Webhooks.Core.Models;

namespace CrestApps.OrchardCore.Webhooks.Core.Indexes;

/// <summary>
/// Finds webhook deliveries by subscription, status and when they are due.
/// </summary>
public sealed class WebhookDeliveryIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the subscription.
    /// </summary>
    public string SubscriptionId { get; set; }

    /// <summary>
    /// Gets or sets the event's name.
    /// </summary>
    public string EventName { get; set; }

    /// <summary>
    /// Gets or sets where the delivery stands.
    /// </summary>
    public WebhookDeliveryStatus Status { get; set; }

    /// <summary>
    /// Gets or sets when it is tried next, while it is pending.
    /// </summary>
    public DateTime? NextAttemptUtc { get; set; }

    /// <summary>
    /// Gets or sets when the event was queued.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
