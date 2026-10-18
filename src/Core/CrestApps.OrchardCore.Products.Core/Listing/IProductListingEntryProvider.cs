namespace CrestApps.OrchardCore.Products.Core.Listing;

/// <summary>
/// Adds filters and sorts to listings that use the automatic ones, for data a module writes to the products' search
/// records through an <see cref="IProductSearchDocumentContributor"/>, such as a customer rating. A filter with no data
/// hides itself.
/// </summary>
public interface IProductListingEntryProvider
{
    /// <summary>
    /// Returns the automatic filters to add.
    /// </summary>
    IEnumerable<ProductListingFacet> GetFacets();

    /// <summary>
    /// Returns the automatic sorts to add.
    /// </summary>
    IEnumerable<ProductListingSort> GetSorts();
}
