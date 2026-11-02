using CrestApps.OrchardCore.Inventory.Core.Indexes;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Inventory.Core.Services;

/// <summary>
/// Stores stock locations.
/// </summary>
public sealed class StockLocationStore : DocumentCatalog<StockLocation, StockLocationIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StockLocationStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public StockLocationStore(ISession session)
        : base(session)
    {
        CollectionName = InventoryConstants.CollectionName;
    }

    /// <summary>
    /// Finds the location a record owns, such as a van's.
    /// </summary>
    /// <param name="ownerKind">The kind of owner.</param>
    /// <param name="ownerId">The owner.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The location, or <see langword="null"/>.</returns>
    public Task<StockLocation> FindByOwnerAsync(string ownerKind, string ownerId, CancellationToken cancellationToken = default)
        => Session.Query<StockLocation, StockLocationIndex>(index => index.OwnerKind == ownerKind && index.OwnerId == ownerId, collection: CollectionName).FirstOrDefaultAsync(cancellationToken);
}

/// <summary>
/// Moves the locations kept in the Inventory settings, from before locations had a store, into the store.
/// </summary>
public static class StockLocationImporter
{
    /// <summary>
    /// Adds the locations the store doesn't have yet, keeping their ids.
    /// </summary>
    /// <param name="legacy">The locations from the settings.</param>
    /// <param name="store">The store.</param>
    /// <param name="createdUtc">When they are added.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>How many were added.</returns>
    public static async Task<int> ImportAsync(IEnumerable<InventoryLocation> legacy, StockLocationStore store, DateTime createdUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);

        var stored = await store.GetAllAsync(cancellationToken);
        var existing = stored.Select(location => location.ItemId).ToHashSet(StringComparer.Ordinal);
        var position = stored.Where(location => location.OwnerKind is null).Select(location => location.Position).DefaultIfEmpty(0).Max();
        var added = 0;

        // They keep the order orders took units from them in.
        foreach (var location in legacy ?? [])
        {
            if (string.IsNullOrEmpty(location?.LocationId) || !existing.Add(location.LocationId))
            {
                continue;
            }

            await store.CreateAsync(new StockLocation
            {
                ItemId = location.LocationId,
                Name = location.Name,
                Address = location.Address,
                Position = ++position,
                CreatedUtc = createdUtc,
            }, cancellationToken);
            added++;
        }

        return added;
    }
}
