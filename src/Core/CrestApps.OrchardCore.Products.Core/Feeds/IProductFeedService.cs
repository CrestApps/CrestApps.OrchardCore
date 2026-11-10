namespace CrestApps.OrchardCore.Products.Core.Feeds;

/// <summary>
/// The store's product feed, which shopping channels and AI agents read: every published product sold online, and each
/// enabled variant of one with variants, priced in a currency. The Product Feeds feature provides it.
/// </summary>
public interface IProductFeedService
{
    /// <summary>
    /// Gets the feed priced in a currency, built at most every 15 minutes, or sooner after stock changes.
    /// </summary>
    /// <param name="currency">The ISO 4217 code of the currency, or <see langword="null"/> for each product's own.</param>
    /// <param name="fresh">Whether to build it now instead of reading the copy kept.</param>
    /// <param name="cancellationToken">A token that cancels the work.</param>
    Task<ProductFeed> GetAsync(string currency, bool fresh = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the items of some products only, now, such as the ones that just changed.
    /// </summary>
    /// <param name="contentItemIds">The content item ids of the products.</param>
    /// <param name="currency">The ISO 4217 code of the currency, or <see langword="null"/> for each product's own.</param>
    /// <param name="cancellationToken">A token that cancels the work.</param>
    Task<ProductFeed> BuildAsync(IEnumerable<string> contentItemIds, string currency, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the items of some products, optionally without Merchant Center's rules, for a reader that shows products
    /// the way the store sells them, such as an AI agent's catalog search: an item with no photo or a relative link
    /// stays in.
    /// </summary>
    /// <param name="contentItemIds">The products' content item ids.</param>
    /// <param name="currency">The currency, or <see langword="null"/> for the store's.</param>
    /// <param name="validate">Whether items Merchant Center would refuse are left out.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<ProductFeed> BuildAsync(IEnumerable<string> contentItemIds, string currency, bool validate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns whether a request may read the feed: the feed is public, or the request carries its key.
    /// </summary>
    /// <param name="key">The key the request carries, or <see langword="null"/>.</param>
    Task<bool> IsAllowedAsync(string key);

    /// <summary>
    /// Returns the key a feed's address has to carry (<c>?key=</c>), or <see langword="null"/> for a public feed, so a
    /// settings page can show a feed's full address.
    /// </summary>
    Task<string> GetKeyAsync();
}
