using CrestApps.OrchardCore.Products.Core.Listing;

namespace CrestApps.OrchardCore.Products.Core.Search;

/// <summary>
/// Keeps another index in step with the product search records.
/// </summary>
/// <remarks>
/// The database records are always written first; they are what a listing falls back to. A handler then writes
/// the same records to a search server, so one product change reaches every index.
/// </remarks>
public interface IProductSearchIndexHandler
{
    /// <summary>
    /// Products were published or changed.
    /// </summary>
    Task IndexedAsync(IReadOnlyCollection<ProductSearchDocument> documents, CancellationToken cancellationToken = default);

    /// <summary>
    /// Products were unpublished, deleted, or are no longer products.
    /// </summary>
    Task RemovedAsync(IReadOnlyCollection<string> contentItemIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every record was rebuilt, because product types or their attributes changed. The index should be written
    /// again from the records.
    /// </summary>
    Task RebuiltAsync(CancellationToken cancellationToken = default);
}
