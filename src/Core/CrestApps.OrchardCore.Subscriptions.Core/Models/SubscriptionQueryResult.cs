namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

/// <summary>
/// Represents a page of subscription sessions and the total matching count.
/// </summary>
public class SubscriptionQueryResult
{
    /// <summary>
    /// Gets or sets the subscription sessions returned by the query.
    /// </summary>
    public IEnumerable<SubscriptionSession> Subscriptions { get; set; }

    /// <summary>
    /// Gets or sets the total number of subscription sessions that match the query.
    /// </summary>
    public int TotalCount { get; set; }
}
