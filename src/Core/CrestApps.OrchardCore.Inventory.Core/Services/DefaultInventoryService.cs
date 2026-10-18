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
/// </remarks>
public sealed class DefaultInventoryService : IInventoryService
{
    private readonly ISession _session;
    private readonly IStockItemStore _items;
    private readonly IStockMovementStore _movements;
    private readonly IClock _clock;
    private readonly IStockLevelObserver[] _observers;

    public DefaultInventoryService(
        ISession session,
        IStockItemStore items,
        IStockMovementStore movements,
        IClock clock,
        IEnumerable<IStockLevelObserver> observers = null)
    {
        _session = session;
        _items = items;
        _movements = movements;
        _clock = clock;
        _observers = observers?.ToArray() ?? [];
    }

    public Task<StockChangeResult> AdjustAsync(string stockItemId, StockMovementKind kind, int quantity, string reason, StockActor actor, CancellationToken cancellationToken = default)
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

        return ChangeAsync(stockItemId, item => quantity, kind, reason, actor, cancellationToken);
    }

    public Task<StockChangeResult> SetCountAsync(string stockItemId, int count, string reason, StockActor actor, CancellationToken cancellationToken = default)
    {
        if (count < 0)
        {
            return Task.FromResult(StockChangeResult.Fail(StockChangeError.InvalidQuantity));
        }

        // A count that matches is recorded too: it says when the stock was last checked.
        return ChangeAsync(stockItemId, item => count - item.OnHand, StockMovementKind.Counted, reason, actor, cancellationToken);
    }

    public async Task<StockChangeResult> RebuildAsync(string stockItemId, CancellationToken cancellationToken = default)
    {
        var item = string.IsNullOrEmpty(stockItemId) ? null : await _items.FindByIdAsync(stockItemId, cancellationToken);

        if (item is null)
        {
            return StockChangeResult.Fail(StockChangeError.NotFound);
        }

        var previous = item.Available;

        item.OnHand = await _movements.SumAsync(stockItemId, cancellationToken);
        item.Reserved = (await QueryReservations(index => index.StockItemId == stockItemId && index.Status == StockReservationStatus.Held)
            .ListAsync(cancellationToken)).Sum(reservation => reservation.Quantity);
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
        var changes = new List<(StockItem Item, int Quantity)>();

        foreach (var (item, quantity) in requested)
        {
            // An order that already holds or bought this item (such as a checkout started twice) isn't held twice.
            if (existing.Any(reservation => reservation.StockItemId == item.ItemId && reservation.Status != StockReservationStatus.Released))
            {
                continue;
            }

            if (!item.AllowBackorder && item.Available < quantity)
            {
                shortages.Add(new StockShortage(item, quantity, Math.Max(0, item.Available)));

                continue;
            }

            changes.Add((item, quantity));
        }

        // All or nothing: a short line refuses the whole order, so nothing is held.
        if (shortages.Count > 0)
        {
            return new StockReservationResult { Shortages = shortages };
        }

        var now = _clock.UtcNow;

        foreach (var (item, quantity) in changes)
        {
            var previous = item.Available;

            item.Reserved += quantity;
            item.ModifiedUtc = now;
            await UpdateAsync(item, previous, cancellationToken);

            await SaveReservationAsync(new StockReservation
            {
                ItemId = UniqueId.GenerateId(),
                StockItemId = item.ItemId,
                OrderId = orderId,
                Quantity = quantity,
                Status = StockReservationStatus.Held,
                ExpiresUtc = expiresUtc,
                CreatedUtc = now,
                ModifiedUtc = now,
            }, cancellationToken);
        }

        return new StockReservationResult();
    }

    public async Task<StockReservationResult> CommitAsync(string orderId, IEnumerable<StockRequest> lines, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(orderId);

        var requested = await ResolveAsync(lines, cancellationToken);
        var reservations = await ListReservationsAsync(orderId, cancellationToken);

        // Holds the order has on items no longer in its lines are sold too: they are what was held for it.
        foreach (var reservation in reservations.Where(reservation => reservation.Status == StockReservationStatus.Held))
        {
            if (!requested.Any(entry => entry.Item.ItemId == reservation.StockItemId) &&
                await _items.FindByIdAsync(reservation.StockItemId, cancellationToken) is { } held)
            {
                requested.Add((held, reservation.Quantity));
            }
        }

        var now = _clock.UtcNow;

        foreach (var (item, quantity) in requested)
        {
            var forItem = reservations.Where(reservation => reservation.StockItemId == item.ItemId).ToArray();

            // Sold already: paying twice, or a retried completion, sells nothing more.
            if (forItem.Any(reservation => reservation.Status == StockReservationStatus.Committed))
            {
                continue;
            }

            var hold = forItem.FirstOrDefault(reservation => reservation.Status == StockReservationStatus.Held);
            var sold = hold?.Quantity ?? quantity;
            var previous = item.Available;

            if (hold is not null)
            {
                item.Reserved = Math.Max(0, item.Reserved - hold.Quantity);
                hold.Status = StockReservationStatus.Committed;
                hold.ModifiedUtc = now;
                await SaveReservationAsync(hold, cancellationToken);
            }
            else
            {
                // No hold left: the order is paid, so the units are sold anyway, and the record keeps them from being
                // sold twice.
                await SaveReservationAsync(new StockReservation
                {
                    ItemId = UniqueId.GenerateId(),
                    StockItemId = item.ItemId,
                    OrderId = orderId,
                    Quantity = sold,
                    Status = StockReservationStatus.Committed,
                    ExpiresUtc = now,
                    CreatedUtc = now,
                    ModifiedUtc = now,
                }, cancellationToken);
            }

            item.OnHand -= sold;
            item.ModifiedUtc = now;
            await UpdateAsync(item, previous, cancellationToken);

            await _movements.CreateAsync(new StockMovement
            {
                ItemId = UniqueId.GenerateId(),
                StockItemId = item.ItemId,
                Kind = StockMovementKind.Sold,
                Quantity = -sold,
                OnHandAfter = item.OnHand,
                OrderId = orderId,
                Utc = now,
            }, cancellationToken);
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

        foreach (var reservation in (await ListReservationsAsync(orderId, cancellationToken)).Where(reservation => reservation.Status == StockReservationStatus.Committed))
        {
            if (await _items.FindByIdAsync(reservation.StockItemId, cancellationToken) is { } item)
            {
                var previous = item.Available;

                item.OnHand += reservation.Quantity;
                item.ModifiedUtc = now;
                await UpdateAsync(item, previous, cancellationToken);

                await _movements.CreateAsync(new StockMovement
                {
                    ItemId = UniqueId.GenerateId(),
                    StockItemId = item.ItemId,
                    Kind = StockMovementKind.Returned,
                    Quantity = reservation.Quantity,
                    OnHandAfter = item.OnHand,
                    OrderId = orderId,
                    Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
                    ActorId = actor?.Id,
                    ActorName = actor?.Name,
                    Utc = now,
                }, cancellationToken);

                returned += reservation.Quantity;
            }

            reservation.Status = StockReservationStatus.Returned;
            reservation.ModifiedUtc = now;
            await SaveReservationAsync(reservation, cancellationToken);
        }

        return returned;
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

    private async Task<int> ReleaseHeldAsync(IReadOnlyList<StockReservation> reservations, string reason, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var released = 0;

        foreach (var reservation in reservations.Where(reservation => reservation.Status == StockReservationStatus.Held))
        {
            if (await _items.FindByIdAsync(reservation.StockItemId, cancellationToken) is { } item)
            {
                var previous = item.Available;

                item.Reserved = Math.Max(0, item.Reserved - reservation.Quantity);
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

    private async Task<StockChangeResult> ChangeAsync(string stockItemId, Func<StockItem, int> delta, StockMovementKind kind, string reason, StockActor actor, CancellationToken cancellationToken)
    {
        var item = string.IsNullOrEmpty(stockItemId) ? null : await _items.FindByIdAsync(stockItemId, cancellationToken);

        if (item is null)
        {
            return StockChangeResult.Fail(StockChangeError.NotFound);
        }

        var now = _clock.UtcNow;
        var quantity = delta(item);
        var previous = item.Available;

        item.OnHand += quantity;
        item.ModifiedUtc = now;

        var movement = new StockMovement
        {
            ItemId = UniqueId.GenerateId(),
            StockItemId = item.ItemId,
            Kind = kind,
            Quantity = quantity,
            OnHandAfter = item.OnHand,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            ActorId = actor?.Id,
            ActorName = actor?.Name,
            Utc = now,
        };

        await _movements.CreateAsync(movement, cancellationToken);
        await UpdateAsync(item, previous, cancellationToken);

        return StockChangeResult.Success(item, movement);
    }

    private async Task UpdateAsync(StockItem item, int previousAvailable, CancellationToken cancellationToken)
    {
        await _items.UpdateAsync(item, cancellationToken);

        foreach (var observer in _observers)
        {
            observer.Changed(item, previousAvailable);
        }
    }

    // The stock items the lines need, with the units each needs (lines for the same item added up). Products that
    // aren't counted have no stock item and are left out.
    private async Task<List<(StockItem Item, int Quantity)>> ResolveAsync(IEnumerable<StockRequest> lines, CancellationToken cancellationToken)
    {
        var requested = new List<(StockItem Item, int Quantity)>();

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
                requested[index] = (item, requested[index].Quantity + line.Quantity);
            }
            else
            {
                requested.Add((item, line.Quantity));
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
}
