using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Webhooks.Core.Indexes;

/// <summary>
/// Finds webhook subscriptions by whether they send events, newest first.
/// </summary>
public sealed class WebhookSubscriptionIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets a value indicating whether the subscription sends events.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets when the subscription was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
