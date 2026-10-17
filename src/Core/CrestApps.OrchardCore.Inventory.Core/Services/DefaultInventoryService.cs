using CrestApps.Core;
using CrestApps.OrchardCore.Inventory.Core.Models;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Inventory.Core.Services;

public sealed class DefaultInventoryService : IInventoryService
{
    internal const string LockPrefix = "STOCK_";

    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromMinutes(1);

    private readonly IStockItemStore _items;
    private readonly IStockMovementStore _movements;
    private readonly IDistributedLock _distributedLock;
    private readonly ISession _session;
    private readonly IClock _clock;

    public DefaultInventoryService(
        IStockItemStore items,
        IStockMovementStore movements,
        IDistributedLock distributedLock,
        ISession session,
        IClock clock)
    {
        _items = items;
        _movements = movements;
        _distributedLock = distributedLock;
        _session = session;
        _clock = clock;
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
        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(LockPrefix + stockItemId, _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return StockChangeResult.Fail(StockChangeError.Busy);
        }

        await using (locker)
        {
            var item = await _items.FindByIdAsync(stockItemId, cancellationToken);

            if (item is null)
            {
                return StockChangeResult.Fail(StockChangeError.NotFound);
            }

            item.OnHand = await _movements.SumAsync(stockItemId, cancellationToken);
            item.ModifiedUtc = _clock.UtcNow;

            await _items.UpdateAsync(item, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);

            return StockChangeResult.Success(item);
        }
    }

    // The item is read, changed and committed while the lock is held, so a change on another server waits and then
    // sees this one.
    private async Task<StockChangeResult> ChangeAsync(string stockItemId, Func<StockItem, int> delta, StockMovementKind kind, string reason, StockActor actor, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(stockItemId))
        {
            return StockChangeResult.Fail(StockChangeError.NotFound);
        }

        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(LockPrefix + stockItemId, _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return StockChangeResult.Fail(StockChangeError.Busy);
        }

        await using (locker)
        {
            var item = await _items.FindByIdAsync(stockItemId, cancellationToken);

            if (item is null)
            {
                return StockChangeResult.Fail(StockChangeError.NotFound);
            }

            var now = _clock.UtcNow;
            var quantity = delta(item);

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
            await _items.UpdateAsync(item, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);

            return StockChangeResult.Success(item, movement);
        }
    }
}
