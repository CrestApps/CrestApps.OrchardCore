namespace CrestApps.OrchardCore.Products.Core.Feeds;

/// <summary>
/// Adds what a module owns to each product feed item, such as stock, weight and size, or the return policy. The feed
/// calls every contributor for each item it prices, before it checks the item.
/// </summary>
public interface IProductFeedItemContributor
{
    /// <summary>
    /// Sets the item's values the module knows.
    /// </summary>
    /// <param name="context">The item, and the product and variant it is.</param>
    /// <param name="cancellationToken">A token that cancels the work.</param>
    Task ContributeAsync(ProductFeedItemContext context, CancellationToken cancellationToken = default);
}
