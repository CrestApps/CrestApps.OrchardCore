namespace CrestApps.OrchardCore.Products.Core.Listing;

/// <summary>
/// Finds published products for a listing page.
/// </summary>
/// <remarks>
/// The default implementation runs SQL over the product search indexes, which works on every database Orchard
/// supports. A search-server implementation can replace it for very large catalogs without changing the listing.
/// </remarks>
public interface IProductSearchService
{
    /// <summary>
    /// Runs the query.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<ProductSearchResult> SearchAsync(ProductSearchQuery query, CancellationToken cancellationToken = default);
}
