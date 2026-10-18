namespace CrestApps.OrchardCore.Products.Core.Listing;

/// <summary>
/// Brings product search records up to date when something other than the product itself changed what they hold,
/// such as stock that sold out. Registered by the Product Listing feature; without it, there are no records to keep.
/// </summary>
public interface IProductSearchIndexRefresher
{
    /// <summary>
    /// Rebuilds the records of these products from their published versions now, in the caller's unit of work. A
    /// product that isn't published, or is gone, loses its record.
    /// </summary>
    Task RefreshAsync(IEnumerable<string> contentItemIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Has the next indexing pass rebuild every record, for a change that touches many products at once.
    /// </summary>
    Task RequestRebuildAsync();
}
