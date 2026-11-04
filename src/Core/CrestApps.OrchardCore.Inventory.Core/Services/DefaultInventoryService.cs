using CrestApps.Core;
using CrestApps.OrchardCore.Inventory.Core.Indexes;
using CrestApps.OrchardCore.Inventory.Core.Models;
using OrchardCore.Modules;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Inventory.Core.Services;

/// <remarks>
/// Everything happens in the caller's session and is committed with the caller's work, never on its own. A separate
/// unit of work can't be used: on SQLite, which allows one writer at a time, it would wait for the caller's open
/// transaction while the caller waits for it.
/// <para>
/// Units are kept per location. An order takes its units of an item from one location that has them all, the one it
/// asked for first; failing that, from each location in turn, in the order the locations are listed.
/// </para>
/// </remarks>
public sealed class DefaultInventoryService : IInventoryService
{
    private readonly ISession _session;
    private readonly IStockItemStore _items;
    private readonly IStockMovementStore _movements;
    private readonly IClock _clock;
    private readonly IStockLevelObserver[] _observers;
    private readonly IInventoryLocationService _locations;

    public DefaultInventoryService(
        ISession session,
        IStockItemStore items,
        IStockMovementStore movements,
        IClock clock,
        IEnumerable<IStockLevelObserver> observers = null,
        IInventoryLocationService locations = null)
    {
        _session = session;
        _items = items;
        _movements = movements;
        _clock = clock;
        _observers = observers?.ToArray() ?? [];
        _locations = locations;
    }

    public Task<StockChangeResult> AdjustAsync(string stockItemId, StockMovementKind kind, int quantity, string reason, StockActor actor, CancellationToken cancellationToken = default)
        => AdjustAsync(stockItemId, null, kind, quantity, reason, actor, cancellationToken);

    public Task<StockChangeResult> AdjustAsync(string stockItemId, string locationId, StockMovementKind kind, int quantity, string reason, StockActor actor, CancellationToken cancellationToken = default)
    {
        var valid = kind switch
        {
            StockMovementKind.Received => quantity > 0,
            StockMovementKind.Adjusted => quantity != 0,
            _ => false,
        };

        if (!valid)
        {
            return Task.FromResult(StockChangeResult.Fail(StockChangeError.InvalidQuantity));
        }

        return ChangeAsync(stockItemId, locationId, level => quantity, kind, reason, actor, purchaseOrderId: null, cancellationToken);
    }

    public async Task<StockChangeResult> ConsumeAsync(string stockItemId, string locationId, int quantity, string referenceType, string referenceId, string reason, StockActor actor, CancellationToken cancellationToken = default)
    {
        if (quantity <= 0)
        {
            return StockChangeResult.Fail(StockChangeError.InvalidQuantity);
        }

        var item = string.IsNullOrEmpty(stockItemId) ? null : await _items.FindByIdAsync(stockItemId, cancellationToken);

        if (item is null)
        {
            return StockChangeResult.Fail(StockChangeError.NotFound);
        }

        // Units held for orders can't be used up.
        if (item.GetLevel(locationId).Available < quantity)
        {
            return StockChangeResult.Fail(StockChangeError.NotEnoughAtLocation);
        }

        return await ChangeAsync(stockItemId, locationId, level => -quantity, StockMovementKind.Consumed, reason, actor, purchaseOrderId: null, (referenceType, referenceId), cancellationToken);
    }

    public Task<StockChangeResult> SetCountAsync(string stockItemId, int count, string reason, StockActor actor, CancellationToken cancellationToken = default)
        => SetCountAsync(stockItemId, null, count, reason, actor, cancellationToken);

    public Task<StockChangeResult> SetCountAsync(string stockItemId, string locationId, int count, string reason, StockActor actor, CancellationToken cancellationToken = default)
    {
        if (count < 0)
        {
            return Task.FromResult(StockChangeResult.Fail(StockChangeError.InvalidQuantity));
        }

        // A count that matches is recorded too: it says when the stock was last checked.
        return ChangeAsync(stockItemId, locationId, level => count - level.OnHand, StockMovementKind.Counted, reason, actor, purchaseOrderId: null, cancellationToken);
    }

    public Task<StockChangeResult> ReceiveAsync(string stockItemId, string locationId, int quantity, string purchaseOrderId, string reason, StockActor actor, CancellationToken cancellationToken = default)
    {
        if (quantity <= 0)
        {
            return Task.FromResult(StockChangeResult.Fail(StockChangeError.InvalidQuantity));
        }

        return ChangeAsync(stockItemId, locationId, level => quantity, StockMovementKind.Received, reason, actor, purchaseOrderId, cancellationToken);
    }

    public async Task<StockChangeResult> TransferAsync(string stockItemId, string fromLocationId, string toLocationId, int quantity, string reason, StockActor actor, CancellationToken cancellationToken = default)
    {
        if (quantity <= 0)
        {
            return StockChangeResult.Fail(StockChangeError.InvalidQuantity);
        }

        var item = string.IsNullOrEmpty(stockItemId) ? null : await _items.FindByIdAsync(stockItemId, cancellationToken);

        if (item is null)
        {
            return StockChangeResult.Fail(StockChangeError.NotFound);
        }

        var from = item.GetLevel(fromLocationId);
        var to = item.GetLevel(toLocationId);

        // Units held for orders stay where they are held.
        if (from == to || from.Available < quantity)
        {
            return StockChangeResult.Fail(StockChangeError.NotEnoughAtLocation);
        }

        var now = _clock.UtcNow;
        var previous = item.Available;

        from.OnHand -= quantity;
        to.OnHand += quantity;
        item.SumLevels();
        item.ModifiedUtc = now;

        await _movements.CreateAsync(Movement(item, from.LocationId, StockMovementKind.Transferred, -quantity, reason, actor, now), cancellationToken);

        var movement = Movement(item, to.LocationId, StockMovementKind.Transferred, quantity, reason, actor, now);

        await _movements.CreateAsync(movement, cancellationToken);
        await UpdateAsync(item, previous, cancellationToken);

        return StockChangeResult.Success(item, movement);
    }

    public async Task<StockChangeResult> RebuildAsync(string stockItemId, CancellationToken cancellationToken = default)
    {
        var item = string.IsNullOrEmpty(stockItemId) ? null : await _items.FindByIdAsync(stockItemId, cancellationToken);

        if (item is null)
        {
            return StockChangeResult.Fail(StockChangeError.NotFound);
        }

        var previous = item.Available;
        var onHand = await _movements.SumByLocationAsync(stockItemId, cancellationToken);
        var reserved = (await QueryReservations(index => index.StockItemId == stockItemId && index.Status == StockReservationStatus.Held).ListAsync(cancellationToken))
            .GroupBy(reservation => LocationOf(reservation.LocationId))
            .ToDictionary(group => group.Key, group => group.Sum(reservation => reservation.Quantity), StringComparer.Ordinal);

        item.Levels = onHand.Keys.Union(reserved.Keys)
            .Select(locationId => new StockLocationLevel
            {
                LocationId = locationId,
                OnHand = onHand.GetValueOrDefault(locationId),
                Reserved = reserved.GetValueOrDefault(locationId),
            })
            .ToList();
        item.SumLevels();
        item.ModifiedUtc = _clock.UtcNow;

        await UpdateAsync(item, previous, cancellationToken);

        return StockChangeResult.Success(item);
    }

    public async Task<StockReservationResult> ReserveAsync(string orderId, IEnumerable<StockRequest> lines, DateTime expiresUtc, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(orderId);

        var requested = await ResolveAsync(lines, cancellationToken);

        if (requested.Count == 0)
        {
            return new StockReservationResult();
        }

        var existing = await ListReservationsAsync(orderId, cancellationToken);
        var shortages = new List<StockShortage>();
        var changes = new List<StockNeed>();

        foreach (var need in requested)
        {
            // An order that already holds or bought this item (such as a checkout started twice) isn't held twice.
            if (existing.Any(reservation => reservation.StockItemId == need.Item.ItemId && reservation.Status != StockReservationStatus.Released))
            {
                continue;
            }

            if (!need.Item.AllowBackorder && need.Item.Available < need.Quantity)
            {
                shortages.Add(new StockShortage(need.Item, need.Quantity, Math.Max(0, need.Item.Available)));

                continue;
            }

            changes.Add(need);
        }

        // All or nothing: a short line refuses the whole order, so nothing is held.
        if (shortages.Count > 0)
        {
            return new StockReservationResult { Shortages = shortages };
        }

        var now = _clock.UtcNow;
        var order = await GetLocationOrderAsync();

        foreach (var need in changes)
        {
            var item = need.Item;
            var previous = item.Available;

            foreach (var (locationId, quantity) in Allocate(item, need.Quantity, need.LocationId, order))
            {
                item.GetLevel(locationId).Reserved += quantity;

                await SaveReservationAsync(new StockReservation
                {
                    ItemId = UniqueId.GenerateId(),
                    StockItemId = item.ItemId,
                    OrderId = orderId,
                    Quantity = quantity,
                    LocationId = locationId,
                    Status = StockReservationStatus.Held,
                    ExpiresUtc = expiresUtc,
                    CreatedUtc = now,
                    ModifiedUtc = now,
                }, cancellationToken);
            }

            item.SumLevels();
            item.ModifiedUtc = now;
            await UpdateAsync(item, previous, cancellationToken);
        }

        return new StockReservationResult();
    }

    public async Task<StockReservationResult> CommitAsync(string orderId, IEnumerable<StockRequest> lines, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(orderId);

        var requested = await ResolveAsync(lines, cancellationToken);
        var reservations = await ListReservationsAsync(orderId, cancellationToken);

        // Holds the order has on items no longer in its lines are sold too: they are what was held for it.
        foreach (var group in reservations.Where(reservation => reservation.Status == StockReservationStatus.Held).GroupBy(reservation => reservation.StockItemId))
        {
            if (!requested.Any(entry => entry.Item.ItemId == group.Key) &&
                await _items.FindByIdAsync(group.Key, cancellationToken) is { } held)
            {
                requested.Add(new StockNeed(held, group.Sum(reservation => reservation.Quantity), null));
            }
        }

        var now = _clock.UtcNow;
        var order = await GetLocationOrderAsync();

        foreach (var need in requested)
        {
            var item = need.Item;
            var forItem = reservations.Where(reservation => reservation.StockItemId == item.ItemId).ToArray();

            // Sold already: paying twice, or a retried completion, sells nothing more.
            if (forItem.Any(reservation => reservation.Status == StockReservationStatus.Committed))
            {
                continue;
            }

            var holds = forItem.Where(reservation => reservation.Status == StockReservationStatus.Held).ToArray();
            var previous = item.Available;
            var sold = new List<(string LocationId, int Quantity)>();

            if (holds.Length > 0)
            {
                // Units were held before the buyer chose where the order is fulfilled from, such as the shop they pick
                // it up at: when that location has them, they're sold from there.
                var moveTo = MoveHoldsTo(item, holds, need.LocationId);

                foreach (var hold in holds)
                {
                    var level = item.GetLevel(hold.LocationId);

                    level.Reserved = Math.Max(0, level.Reserved - hold.Quantity);
                    level = moveTo is null ? level : item.GetLevel(moveTo);
                    hold.LocationId = level.LocationId;
                    hold.Status = StockReservationStatus.Committed;
                    hold.ModifiedUtc = now;
                    await SaveReservationAsync(hold, cancellationToken);

                    sold.Add((level.LocationId, hold.Quantity));
                }
            }
            else
            {
                // No hold left: the order is paid, so the units are sold anyway, and the record keeps them from being
                // sold twice.
                foreach (var (locationId, quantity) in Allocate(item, need.Quantity, need.LocationId, order))
                {
                    await SaveReservationAsync(new StockReservation
                    {
                        ItemId = UniqueId.GenerateId(),
                        StockItemId = item.ItemId,
                        OrderId = orderId,
                        Quantity = quantity,
                        LocationId = locationId,
                        Status = StockReservationStatus.Committed,
                        ExpiresUtc = now,
                        CreatedUtc = now,
                        ModifiedUtc = now,
                    }, cancellationToken);

                    sold.Add((locationId, quantity));
                }
            }

            foreach (var (locationId, quantity) in sold)
            {
                item.GetLevel(locationId).OnHand -= quantity;
                item.SumLevels();

                var movement = Movement(item, locationId, StockMovementKind.Sold, -quantity, reason: null, actor: null, now);
                movement.OrderId = orderId;

                await _movements.CreateAsync(movement, cancellationToken);
            }

            item.ModifiedUtc = now;
            await UpdateAsync(item, previous, cancellationToken);
        }

        return new StockReservationResult();
    }

    public async Task<StockReservationResult> ReleaseAsync(string orderId, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(orderId);

        await ReleaseHeldAsync(await ListReservationsAsync(orderId, cancellationToken), reason, cancellationToken);

        return new StockReservationResult();
    }

    public async Task<int> RestockAsync(string orderId, string reason, StockActor actor, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(orderId);

        var now = _clock.UtcNow;
        var returned = 0;

        // Units go back to the location they were sold from, less any that came back with a return already.
        foreach (var reservation in (await ListReservationsAsync(orderId, cancellationToken)).Where(reservation => reservation.Status == StockReservationStatus.Committed))
        {
            var quantity = reservation.Quantity - reservation.ReturnedQuantity;

            if (quantity > 0 && await _items.FindByIdAsync(reservation.StockItemId, cancellationToken) is { } item)
            {
                await ReturnUnitsAsync(item, reservation.LocationId, quantity, orderId, reason, actor, now, cancellationToken);

                returned += quantity;
            }

            reservation.ReturnedQuantity = reservation.Quantity;
            reservation.Status = StockReservationStatus.Returned;
            reservation.ModifiedUtc = now;
            await SaveReservationAsync(reservation, cancellationToken);
        }

        return returned;
    }

    public async Task<int> RestockAsync(string orderId, IEnumerable<StockRequest> lines, string locationId, string reason, StockActor actor, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(orderId);

        var now = _clock.UtcNow;
        var returned = 0;
        var reservations = (await ListReservationsAsync(orderId, cancellationToken)).Where(reservation => reservation.Status == StockReservationStatus.Committed).ToArray();

        foreach (var need in await ResolveAsync(lines, cancellationToken))
        {
            var left = need.Quantity;

            foreach (var reservation in reservations.Where(reservation => reservation.StockItemId == need.Item.ItemId))
            {
                var quantity = Math.Min(left, reservation.Quantity - reservation.ReturnedQuantity);

                if (quantity <= 0)
                {
                    continue;
                }

                await ReturnUnitsAsync(need.Item, string.IsNullOrEmpty(locationId) ? reservation.LocationId : locationId, quantity, orderId, reason, actor, now, cancellationToken);

                reservation.ReturnedQuantity += quantity;

                if (reservation.ReturnedQuantity >= reservation.Quantity)
                {
                    reservation.Status = StockReservationStatus.Returned;
                }

                reservation.ModifiedUtc = now;
                await SaveReservationAsync(reservation, cancellationToken);

                returned += quantity;
                left -= quantity;

                if (left == 0)
                {
                    break;
                }
            }
        }

        return returned;
    }

    private async Task ReturnUnitsAsync(StockItem item, string locationId, int quantity, string orderId, string reason, StockActor actor, DateTime now, CancellationToken cancellationToken)
    {
        var previous = item.Available;
        var level = item.GetLevel(locationId);

        level.OnHand += quantity;
        item.SumLevels();
        item.ModifiedUtc = now;
        await UpdateAsync(item, previous, cancellationToken);

        var movement = Movement(item, level.LocationId, StockMovementKind.Returned, quantity, reason, actor, now);
        movement.OrderId = orderId;

        await _movements.CreateAsync(movement, cancellationToken);
    }

    public async Task<int> ReleaseExpiredAsync(int take, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var expired = await QueryReservations(index => index.Status == StockReservationStatus.Held && index.ExpiresUtc < now)
            .OrderBy(index => index.ExpiresUtc)
            .Take(take)
            .ListAsync(cancellationToken);

        return await ReleaseHeldAsync(expired.ToArray(), "The hold expired before the order was paid.", cancellationToken);
    }

    /// <summary>
    /// Where an order's units of an item come from: one location that has them all, the one asked for first, then
    /// the locations in their order, then any location no longer listed that still holds units. Failing that, from
    /// each in turn; units no location has (a backorder) are owed by the first.
    /// </summary>
    internal static IReadOnlyList<(string LocationId, int Quantity)> Allocate(StockItem item, int quantity, string preferredLocationId, IReadOnlyList<string> locationOrder)
    {
        var candidates = new List<string>();

        if (!string.IsNullOrEmpty(preferredLocationId))
        {
            candidates.Add(preferredLocationId);
        }

        candidates.AddRange(locationOrder);
        item.NormalizeLevels();
        candidates.AddRange(item.Levels.Select(level => level.LocationId));
        candidates = candidates.Select(LocationOf).Distinct(StringComparer.Ordinal).ToList();

        int AvailableAt(string locationId)
            => Math.Max(0, item.FindLevel(locationId)?.Available ?? 0);

        var whole = candidates.FirstOrDefault(locationId => AvailableAt(locationId) >= quantity);

        if (whole is not null)
        {
            return [(whole, quantity)];
        }

        var parts = new List<(string LocationId, int Quantity)>();
        var left = quantity;

        foreach (var locationId in candidates)
        {
            var take = Math.Min(left, AvailableAt(locationId));

            if (take > 0)
            {
                parts.Add((locationId, take));
                left -= take;
            }

            if (left == 0)
            {
                break;
            }
        }

        if (left > 0)
        {
            var first = candidates[0];
            var index = parts.FindIndex(part => part.LocationId == first);

            if (index >= 0)
            {
                parts[index] = (first, parts[index].Quantity + left);
            }
            else
            {
                parts.Insert(0, (first, left));
            }
        }

        return parts;
    }

    // The location holds move to when the order is fulfilled from another one that has all the units, or null.
    private static string MoveHoldsTo(StockItem item, IReadOnlyList<StockReservation> holds, string preferredLocationId)
    {
        if (string.IsNullOrEmpty(preferredLocationId) || holds.All(hold => LocationOf(hold.LocationId) == preferredLocationId))
        {
            return null;
        }

        var elsewhere = holds.Where(hold => LocationOf(hold.LocationId) != preferredLocationId).Sum(hold => hold.Quantity);

        return (item.FindLevel(preferredLocationId)?.Available ?? 0) >= elsewhere ? preferredLocationId : null;
    }

    private async Task<IReadOnlyList<string>> GetLocationOrderAsync()
        => _locations is null
            ? [InventoryConstants.DefaultLocationId]
            : (await _locations.GetLocationsAsync()).Select(location => location.LocationId).DefaultIfEmpty(InventoryConstants.DefaultLocationId).ToArray();

    private async Task<int> ReleaseHeldAsync(IReadOnlyList<StockReservation> reservations, string reason, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var released = 0;

        foreach (var reservation in reservations.Where(reservation => reservation.Status == StockReservationStatus.Held))
        {
            if (await _items.FindByIdAsync(reservation.StockItemId, cancellationToken) is { } item)
            {
                var previous = item.Available;
                var level = item.GetLevel(reservation.LocationId);

                level.Reserved = Math.Max(0, level.Reserved - reservation.Quantity);
                item.SumLevels();
                item.ModifiedUtc = now;
                await UpdateAsync(item, previous, cancellationToken);
            }

            reservation.Status = StockReservationStatus.Released;
            reservation.ReleaseReason = reason;
            reservation.ModifiedUtc = now;
            await SaveReservationAsync(reservation, cancellationToken);
            released++;
        }

        return released;
    }

    private Task<StockChangeResult> ChangeAsync(string stockItemId, string locationId, Func<StockLocationLevel, int> delta, StockMovementKind kind, string reason, StockActor actor, string purchaseOrderId, CancellationToken cancellationToken)
        => ChangeAsync(stockItemId, locationId, delta, kind, reason, actor, purchaseOrderId, (null, null), cancellationToken);

    private async Task<StockChangeResult> ChangeAsync(string stockItemId, string locationId, Func<StockLocationLevel, int> delta, StockMovementKind kind, string reason, StockActor actor, string purchaseOrderId, (string Type, string Id) reference, CancellationToken cancellationToken)
    {
        var item = string.IsNullOrEmpty(stockItemId) ? null : await _items.FindByIdAsync(stockItemId, cancellationToken);

        if (item is null)
        {
            return StockChangeResult.Fail(StockChangeError.NotFound);
        }

        var now = _clock.UtcNow;
        var level = item.GetLevel(locationId);
        var quantity = delta(level);
        var previous = item.Available;

        level.OnHand += quantity;
        item.SumLevels();
        item.ModifiedUtc = now;

        var movement = Movement(item, level.LocationId, kind, quantity, reason, actor, now);
        movement.PurchaseOrderId = purchaseOrderId;
        movement.ReferenceType = reference.Type;
        movement.ReferenceId = reference.Id;

        await _movements.CreateAsync(movement, cancellationToken);
        await UpdateAsync(item, previous, cancellationToken);

        return StockChangeResult.Success(item, movement);
    }

    private static StockMovement Movement(StockItem item, string locationId, StockMovementKind kind, int quantity, string reason, StockActor actor, DateTime now)
        => new()
        {
            ItemId = UniqueId.GenerateId(),
            StockItemId = item.ItemId,
            LocationId = LocationOf(locationId),
            Kind = kind,
            Quantity = quantity,
            OnHandAfter = item.OnHand,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            ActorId = actor?.Id,
            ActorName = actor?.Name,
            Utc = now,
        };

    private static string LocationOf(string locationId)
        => string.IsNullOrEmpty(locationId) ? InventoryConstants.DefaultLocationId : locationId;

    private async Task UpdateAsync(StockItem item, int previousAvailable, CancellationToken cancellationToken)
    {
        await _items.UpdateAsync(item, cancellationToken);

        foreach (var observer in _observers)
        {
            observer.Changed(item, previousAvailable);
        }
    }

    // The stock items the lines need, with the units each needs (lines for the same item added up) and the location
    // the order would rather they came from. Products that aren't counted have no stock item and are left out.
    private async Task<List<StockNeed>> ResolveAsync(IEnumerable<StockRequest> lines, CancellationToken cancellationToken)
    {
        var requested = new List<StockNeed>();

        foreach (var line in lines ?? [])
        {
            if (line is null || line.Quantity <= 0 || string.IsNullOrEmpty(line.ContentItemId))
            {
                continue;
            }

            var item = await _items.FindAsync(line.ContentItemId, line.VariantId, cancellationToken: cancellationToken);

            if (item is null || item.Archived)
            {
                continue;
            }

            var index = requested.FindIndex(entry => entry.Item.ItemId == item.ItemId);

            if (index >= 0)
            {
                requested[index] = requested[index] with
                {
                    Quantity = requested[index].Quantity + line.Quantity,
                    LocationId = requested[index].LocationId ?? line.LocationId,
                };
            }
            else
            {
                requested.Add(new StockNeed(item, line.Quantity, line.LocationId));
            }
        }

        return requested;
    }

    private async Task<IReadOnlyList<StockReservation>> ListReservationsAsync(string orderId, CancellationToken cancellationToken)
        => (await QueryReservations(index => index.OrderId == orderId).ListAsync(cancellationToken)).ToArray();

    private IQuery<StockReservation, StockReservationIndex> QueryReservations(System.Linq.Expressions.Expression<Func<StockReservationIndex, bool>> predicate)
        => _session.Query<StockReservation, StockReservationIndex>(predicate, collection: InventoryConstants.CollectionName);

    private Task SaveReservationAsync(StockReservation reservation, CancellationToken cancellationToken)
        => _session.SaveAsync(reservation, false, InventoryConstants.CollectionName, cancellationToken);

    private sealed record StockNeed(StockItem Item, int Quantity, string LocationId);
}
