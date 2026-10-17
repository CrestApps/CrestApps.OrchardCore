using CrestApps.OrchardCore.Inventory.Core.Indexes;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Inventory.Core.Services;

public interface IStockItemStore
{
    ValueTask<StockItem> FindByIdAsync(string stockItemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the stock item counting a product, or one of its variants, at a location.
    /// </summary>
    Task<StockItem> FindAsync(string contentItemId, string variantId, string locationId = InventoryConstants.DefaultLocationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists every stock item of a product, archived ones included.
    /// </summary>
    Task<IReadOnlyList<StockItem>> ListByProductAsync(string contentItemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the stock item, not archived, at a location with this SKU, ignoring case. A SKU that more than one item
    /// shares finds none, so a count is never applied to the wrong one.
    /// </summary>
    Task<StockItem> FindBySkuAsync(string sku, string locationId = InventoryConstants.DefaultLocationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists a page of the stock items that aren't archived, by title, for an export.
    /// </summary>
    Task<IReadOnlyList<StockItem>> ListForExportAsync(int skip, int take, CancellationToken cancellationToken = default);

    Task<(int Count, IReadOnlyList<StockItem> Items)> PageAsync(StockItemQuery query, int skip, int take, CancellationToken cancellationToken = default);

    ValueTask CreateAsync(StockItem item, CancellationToken cancellationToken = default);

    ValueTask UpdateAsync(StockItem item, CancellationToken cancellationToken = default);
}

public sealed class StockItemQuery
{
    /// <summary>
    /// Gets or sets the start of a SKU or title to find.
    /// </summary>
    public string Search { get; set; }

    public StockItemFilter Filter { get; set; }

    public bool IncludeArchived { get; set; }
}

public enum StockItemFilter
{
    All = 0,

    /// <summary>
    /// Available units at or below the item's low-stock threshold, out of stock included.
    /// </summary>
    Low = 1,

    /// <summary>
    /// No units available.
    /// </summary>
    Out = 2,

    /// <summary>
    /// Sold below zero, as backorders.
    /// </summary>
    Backordered = 3,
}

public sealed class StockItemStore : DocumentCatalog<StockItem, StockItemIndex>, IStockItemStore
{
    // An adjustment and a checkout must not overwrite each other's counts.
    protected override bool CheckConcurrency => true;

    public StockItemStore(ISession session)
        : base(session)
    {
        CollectionName = InventoryConstants.CollectionName;
    }

    public Task<StockItem> FindAsync(string contentItemId, string variantId, string locationId = InventoryConstants.DefaultLocationId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(contentItemId))
        {
            return Task.FromResult<StockItem>(null);
        }

        locationId ??= InventoryConstants.DefaultLocationId;

        var query = string.IsNullOrEmpty(variantId)
            ? Session.Query<StockItem, StockItemIndex>(index => index.ContentItemId == contentItemId && index.VariantId == null && index.LocationId == locationId, collection: CollectionName)
            : Session.Query<StockItem, StockItemIndex>(index => index.ContentItemId == contentItemId && index.VariantId == variantId && index.LocationId == locationId, collection: CollectionName);

        return query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StockItem>> ListByProductAsync(string contentItemId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(contentItemId))
        {
            return [];
        }

        var items = await Session.Query<StockItem, StockItemIndex>(index => index.ContentItemId == contentItemId, collection: CollectionName)
            .ListAsync(cancellationToken);

        return items.ToArray();
    }

    public async Task<StockItem> FindBySkuAsync(string sku, string locationId = InventoryConstants.DefaultLocationId, CancellationToken cancellationToken = default)
    {
        if (StockItemIndexProvider.Normalize(sku) is not { } normalized)
        {
            return null;
        }

        locationId ??= InventoryConstants.DefaultLocationId;

        var items = (await Session.Query<StockItem, StockItemIndex>(index =>
                index.NormalizedSku == normalized && !index.Archived && index.LocationId == locationId,
                collection: CollectionName)
            .Take(2)
            .ListAsync(cancellationToken)).ToArray();

        return items.Length == 1 ? items[0] : null;
    }

    public async Task<IReadOnlyList<StockItem>> ListForExportAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        var items = await Session.Query<StockItem, StockItemIndex>(index => !index.Archived, collection: CollectionName)
            .OrderBy(index => index.NormalizedTitle)
            .ThenBy(index => index.Id)
            .Skip(skip)
            .Take(take)
            .ListAsync(cancellationToken);

        return items.ToArray();
    }

    public async Task<(int Count, IReadOnlyList<StockItem> Items)> PageAsync(StockItemQuery query, int skip, int take, CancellationToken cancellationToken = default)
    {
        query ??= new StockItemQuery();

        var items = Session.Query<StockItem, StockItemIndex>(collection: CollectionName);

        if (!query.IncludeArchived)
        {
            items = items.Where(index => !index.Archived);
        }

        items = query.Filter switch
        {
            StockItemFilter.Low => items.Where(index => index.IsLow),
            StockItemFilter.Out => items.Where(index => index.Available <= 0),
            StockItemFilter.Backordered => items.Where(index => index.Available < 0),
            _ => items,
        };

        if (StockItemIndexProvider.Normalize(query.Search) is { } search)
        {
            items = items.Where(index => index.NormalizedSku.StartsWith(search) || index.NormalizedTitle.StartsWith(search));
        }

        var count = await items.CountAsync(cancellationToken);

        // The fewest available first, so what needs attention is at the top.
        var page = await items.OrderBy(index => index.Available).ThenBy(index => index.NormalizedTitle).Skip(skip).Take(take).ListAsync(cancellationToken);

        return (count, page.ToArray());
    }
}
