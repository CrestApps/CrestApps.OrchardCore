using CrestApps.OrchardCore.Products.Core.Listing;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.Products.Core.Search;

/// <summary>
/// Searches a product search index held by a search server.
/// </summary>
/// <remarks>
/// Registered as a keyed service under the Orchard Core indexing provider's name, such as <c>Elasticsearch</c>.
/// It answers the same query the database search does, with the same filter counts, ranges, sorts and paging; a
/// listing falls back to the database search when it throws.
/// </remarks>
public interface IProductIndexSearchService
{
    Task<ProductSearchResult> SearchAsync(IndexProfile indexProfile, ProductSearchQuery query, CancellationToken cancellationToken = default);
}
