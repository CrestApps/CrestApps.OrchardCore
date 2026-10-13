namespace CrestApps.OrchardCore.Products.Core.Listing;

/// <summary>
/// How a listing moves through its pages. Every mode keeps real page links, so a listing works without JavaScript
/// and crawlers reach every page.
/// </summary>
public enum ProductListingPaging
{
    /// <summary>
    /// Numbered pages.
    /// </summary>
    Pages = 0,

    /// <summary>
    /// A button that adds the next page's products below the ones shown.
    /// </summary>
    LoadMore = 1,

    /// <summary>
    /// The next page's products are added as the visitor scrolls to the end.
    /// </summary>
    InfiniteScroll = 2,
}
