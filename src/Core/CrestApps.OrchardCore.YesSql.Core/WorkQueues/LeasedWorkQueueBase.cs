using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.WorkQueues;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.YesSql.Core.WorkQueues;

/// <summary>
/// The rules of a leased work queue: when an item can be claimed, what a claim and each settlement change, and that
/// only the current owner settles. A derived queue only says how items are loaded, saved and found due.
/// </summary>
/// <typeparam name="T">The work item type.</typeparam>
public abstract class LeasedWorkQueueBase<T> : ILeasedWorkQueue<T>
    where T : CatalogItem, ILeasedWorkItem
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LeasedWorkQueueBase{T}"/> class.
    /// </summary>
    /// <param name="clock">The clock.</param>
    /// <param name="options">How the queue stores and retires its items.</param>
    protected LeasedWorkQueueBase(IClock clock, LeasedWorkQueueOptions options)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(options);

        Clock = clock;
        Options = options;
    }

    /// <summary>
    /// Gets the clock.
    /// </summary>
    protected IClock Clock { get; }

    /// <summary>
    /// Gets how the queue stores and retires its items.
    /// </summary>
    protected LeasedWorkQueueOptions Options { get; }

    /// <inheritdoc/>
    public async Task EnqueueAsync(T item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        var now = Clock.UtcNow;

        if (string.IsNullOrEmpty(item.ItemId))
        {
            item.ItemId = UniqueId.GenerateId();
        }

        item.Status = LeasedWorkItemStatus.Pending;
        item.OwnerToken = null;

        if (item.NextAttemptUtc == default)
        {
            item.NextAttemptUtc = now;
        }

        Touch(item, now);

        await AddAsync(item, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<T>> ClaimDueAsync(int batchSize, TimeSpan lease, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lease, TimeSpan.Zero);

        var candidateIds = await ListDueIdsAsync(Clock.UtcNow, batchSize, cancellationToken);
        var claimed = new List<T>(candidateIds.Count);

        foreach (var itemId in candidateIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var item = await TryClaimAsync(itemId, lease, cancellationToken);

            if (item is not null)
            {
                claimed.Add(item);
            }
        }

        return claimed;
    }

    /// <inheritdoc/>
    public Task<T> TryClaimAsync(string itemId, TimeSpan lease, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(itemId);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lease, TimeSpan.Zero);

        return ChangeAsync(itemId, (item, now) =>
        {
            if (!IsClaimable(item, now))
            {
                return false;
            }

            item.Status = LeasedWorkItemStatus.Claimed;
            item.OwnerToken = Guid.NewGuid().ToString("N");
            item.FenceToken++;
            item.NextAttemptUtc = now.Add(lease);

            return true;
        }, remove: false, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> CompleteAsync(T claimed, Action<T> update = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(claimed);

        var settled = await SettleAsync(claimed, (item, now) =>
        {
            item.Status = LeasedWorkItemStatus.Completed;
            item.OwnerToken = null;
            item.LastError = null;
            item.NextAttemptUtc = now;
            update?.Invoke(item);
        }, Options.RemoveCompletedItems, cancellationToken);

        return settled is not null;
    }

    /// <inheritdoc/>
    public async Task<LeasedWorkItemStatus?> RetryAsync(
        T claimed,
        string error,
        Func<int, TimeSpan> backoff,
        bool countAttempt = true,
        Action<T> update = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(claimed);
        ArgumentNullException.ThrowIfNull(backoff);

        var settled = await SettleAsync(claimed, (item, now) =>
        {
            if (countAttempt)
            {
                item.AttemptCount++;
            }

            item.LastError = error;
            item.OwnerToken = null;

            if (countAttempt && item.AttemptCount >= Options.MaxAttempts)
            {
                item.Status = LeasedWorkItemStatus.DeadLettered;
            }
            else
            {
                var wait = backoff(Math.Max(1, item.AttemptCount));

                item.Status = LeasedWorkItemStatus.Pending;
                item.NextAttemptUtc = now.Add(wait < TimeSpan.Zero ? TimeSpan.Zero : wait);
            }

            update?.Invoke(item);
        }, remove: false, cancellationToken);

        return settled?.Status;
    }

    /// <inheritdoc/>
    public async Task<bool> AbandonAsync(T claimed, string reason, Action<T> update = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(claimed);

        var settled = await SettleAsync(claimed, (item, _) =>
        {
            item.Status = LeasedWorkItemStatus.DeadLettered;
            item.OwnerToken = null;
            item.LastError = reason;
            update?.Invoke(item);
        }, remove: false, cancellationToken);

        return settled is not null;
    }

    /// <summary>
    /// Gets whether an item can be claimed at <paramref name="nowUtc"/>.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="nowUtc">The current UTC time.</param>
    /// <returns><see langword="true"/> when the item is pending or its lease expired, and it is due.</returns>
    public static bool IsClaimable(T item, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(item);

        return item.Status is LeasedWorkItemStatus.Pending or LeasedWorkItemStatus.Claimed &&
            item.NextAttemptUtc <= nowUtc;
    }

    /// <summary>
    /// Adds a new item in the caller's unit of work, without saving it.
    /// </summary>
    /// <param name="item">The item, already pending.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    protected abstract Task AddAsync(T item, CancellationToken cancellationToken);

    /// <summary>
    /// Lists the identifiers of the items due at <paramref name="nowUtc"/>, oldest first: pending items whose retry time
    /// has come and claimed items whose lease expired.
    /// </summary>
    /// <param name="nowUtc">The current UTC time.</param>
    /// <param name="maxCount">The most identifiers to return.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The identifiers.</returns>
    protected abstract Task<IReadOnlyList<string>> ListDueIdsAsync(DateTime nowUtc, int maxCount, CancellationToken cancellationToken);

    /// <summary>
    /// Loads an item, applies a change when <paramref name="change"/> accepts it, and saves (or removes) it with an
    /// optimistic concurrency check, committing at once.
    /// </summary>
    /// <param name="itemId">The item identifier.</param>
    /// <param name="change">Changes the loaded item and returns <see langword="true"/>, or returns <see langword="false"/> to leave it unchanged.</param>
    /// <param name="remove">Whether to remove the item instead of saving it after the change.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The changed item, or <see langword="null"/> when it does not exist, the change was declined, or another worker won the race.</returns>
    protected abstract Task<T> CommitAsync(string itemId, Func<T, bool> change, bool remove, CancellationToken cancellationToken);

    private Task<T> ChangeAsync(string itemId, Func<T, DateTime, bool> change, bool remove, CancellationToken cancellationToken)
        => CommitAsync(itemId, item =>
        {
            var now = Clock.UtcNow;

            if (!change(item, now))
            {
                return false;
            }

            Touch(item, now);

            return true;
        }, remove, cancellationToken);

    private Task<T> SettleAsync(T claimed, Action<T, DateTime> settle, bool remove, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(claimed.ItemId);

        var ownerToken = claimed.OwnerToken;
        var fenceToken = claimed.FenceToken;

        return ChangeAsync(claimed.ItemId, (item, now) =>
        {
            // Only the worker that still holds the claim settles it. After its lease expired and another worker claimed
            // the item, the owner and fence tokens have moved on and this late settlement is refused.
            if (item.Status != LeasedWorkItemStatus.Claimed ||
                string.IsNullOrEmpty(ownerToken) ||
                !string.Equals(item.OwnerToken, ownerToken, StringComparison.Ordinal) ||
                item.FenceToken != fenceToken)
            {
                return false;
            }

            settle(item, now);

            return true;
        }, remove, cancellationToken);
    }

    private static void Touch(T item, DateTime now)
    {
        if (item is IModifiedUtcAwareModel modified)
        {
            modified.ModifiedUtc = now;
        }
    }
}
