using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// The YesSql index of web push subscriptions.
/// </summary>
public sealed class WebPushSubscriptionIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the worker.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets the SHA-256 of the endpoint, which can be longer than an index column.
    /// </summary>
    public string EndpointHash { get; set; }
}
