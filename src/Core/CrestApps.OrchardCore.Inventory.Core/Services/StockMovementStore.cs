using CrestApps.OrchardCore.Inventory.Core.Indexes;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Inventory.Core.Services;

/// <summary>
/// The ledger: movements are added and read, never changed or removed.
/// </summary>
public interface IStockMovementStore
{
    ValueTask CreateAsync(StockMovement movement, CancellationToken cancellationToken = default);

    /// <summary>
    /// A page of a stock item's movements, newest first.
    /// </summary>
    Task<(int Count, IReadOnlyList<StockMovement> Movements)> PageAsync(string stockItemId, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// The sum of a stock item's movements: the units it holds by its history.
    /// </summary>
    Task<int> SumAsync(string stockItemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds up the movements of a stock item at each location; a movement without a location is at the default one.
    /// </summary>
    Task<IReadOnlyDictionary<string, int>> SumByLocationAsync(string stockItemId, CancellationToken cancellationToken = default);
}

public sealed class StockMovementStore : DocumentCatalog<StockMovement, StockMovementIndex>, IStockMovementStore
{
    private const int SumPageSize = 500;

    public StockMovementStore(ISession session)
        : base(session)
    {
        CollectionName = InventoryConstants.CollectionName;
    }

    public async Task<(int Count, IReadOnlyList<StockMovement> Movements)> PageAsync(string stockItemId, int skip, int take, CancellationToken cancellationToken = default)
    {
        var movements = Session.Query<StockMovement, StockMovementIndex>(index => index.StockItemId == stockItemId, collection: CollectionName);

        var count = await movements.CountAsync(cancellationToken);
        var page = await movements.OrderByDescending(index => index.Utc).ThenByDescending(index => index.Id).Skip(skip).Take(take).ListAsync(cancellationToken);

        return (count, page.ToArray());
    }

    public async Task<int> SumAsync(string stockItemId, CancellationToken cancellationToken = default)
        => (await SumByLocationAsync(stockItemId, cancellationToken)).Values.Sum();

    public async Task<IReadOnlyDictionary<string, int>> SumByLocationAsync(string stockItemId, CancellationToken cancellationToken = default)
    {
        var sums = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var skip = 0; ; skip += SumPageSize)
        {
            var page = (await Session.Query<StockMovement, StockMovementIndex>(index => index.StockItemId == stockItemId, collection: CollectionName)
                .OrderBy(index => index.Id)
                .Skip(skip)
                .Take(SumPageSize)
                .ListAsync(cancellationToken)).ToArray();

            foreach (var movement in page)
            {
                var locationId = string.IsNullOrEmpty(movement.LocationId) ? InventoryConstants.DefaultLocationId : movement.LocationId;

                sums[locationId] = sums.GetValueOrDefault(locationId) + movement.Quantity;
            }

            if (page.Length < SumPageSize)
            {
                return sums;
            }
        }
    }
}
