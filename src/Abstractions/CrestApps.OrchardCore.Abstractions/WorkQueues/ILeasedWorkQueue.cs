namespace CrestApps.OrchardCore.WorkQueues;

/// <summary>
/// A durable queue of <see cref="ILeasedWorkItem"/>s that several nodes drain together: an item is claimed by one
/// worker at a time under a lease, retried with a back-off when it fails, and given up on when its attempts run out.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="EnqueueAsync"/> joins the caller's unit of work, so an item is stored together with the change that
/// produced it (the outbox pattern). Claims and settlements commit on their own, in a session of their own: a lost
/// race never breaks the caller's session, and the worker's own failure never rolls back the queue's bookkeeping. Call
/// them while the caller's unit of work has no unsaved writes.
/// </para>
/// <para>
/// Claims are exclusive: an item is claimed only while it is due, and the claim is saved with an optimistic
/// concurrency check, so two nodes never both own it. A settlement succeeds only while the stored owner and fence
/// tokens are still the ones the worker claimed with; after the lease expired and another worker claimed the item,
/// the late settlement is refused.
/// </para>
/// </remarks>
/// <typeparam name="T">The work item type.</typeparam>
public interface ILeasedWorkQueue<T>
    where T : class, ILeasedWorkItem
{
    /// <summary>
    /// Adds an item to the queue as pending, due at its <see cref="ILeasedWorkItem.NextAttemptUtc"/> (now when it is
    /// not set), in the caller's unit of work. The caller saves it.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task EnqueueAsync(T item, CancellationToken cancellationToken = default);

    /// <summary>
    /// Claims up to <paramref name="batchSize"/> due items, oldest first. Items another worker claims first are left
    /// out, except on a queue that commits in the caller's session, where the lost race throws.
    /// </summary>
    /// <param name="batchSize">The most items to claim.</param>
    /// <param name="lease">How long each claim lasts before the item can be claimed again.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The claimed items, each carrying its new owner and fence tokens.</returns>
    Task<IReadOnlyList<T>> ClaimDueAsync(int batchSize, TimeSpan lease, CancellationToken cancellationToken = default);

    /// <summary>
    /// Claims one item when it is due.
    /// </summary>
    /// <param name="itemId">The item identifier.</param>
    /// <param name="lease">How long the claim lasts before the item can be claimed again.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The claimed item, or <see langword="null"/> when it does not exist, is not due, or another worker claimed it first.</returns>
    Task<T> TryClaimAsync(string itemId, TimeSpan lease, CancellationToken cancellationToken = default);

    /// <summary>
    /// Settles a claim as done: the item is removed, or kept as <see cref="LeasedWorkItemStatus.Completed"/> by queues
    /// that keep completed items.
    /// </summary>
    /// <param name="claimed">The item as it was claimed; its identifier, owner token and fence token identify the claim.</param>
    /// <param name="update">Changes to apply to the stored item in the same save; optional.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the claim was still held and is settled; <see langword="false"/> when it was lost.</returns>
    Task<bool> CompleteAsync(T claimed, Action<T> update = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Settles a claim as failed: the item goes back to pending, due after the back-off, or is dead-lettered when it
    /// has used up the queue's attempts.
    /// </summary>
    /// <param name="claimed">The item as it was claimed.</param>
    /// <param name="error">The error to record.</param>
    /// <param name="backoff">
    /// Computes how long to wait before the next attempt from the item's attempt count after this failure, read from
    /// the stored item (see <see cref="LeasedWorkBackoff"/>).
    /// </param>
    /// <param name="countAttempt">
    /// Whether the failure uses up an attempt. Pass <see langword="false"/> when the work could not even start, for
    /// example because the handler it needs is not enabled yet.
    /// </param>
    /// <param name="update">Changes to apply to the stored item in the same save, after its new state is set; optional.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// <see cref="LeasedWorkItemStatus.Pending"/> when the item will be retried,
    /// <see cref="LeasedWorkItemStatus.DeadLettered"/> when its attempts ran out, or <see langword="null"/> when the
    /// claim was lost.
    /// </returns>
    Task<LeasedWorkItemStatus?> RetryAsync(
        T claimed,
        string error,
        Func<int, TimeSpan> backoff,
        bool countAttempt = true,
        Action<T> update = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Settles a claim by giving up on the item: it is dead-lettered with the reason, whatever its attempts.
    /// </summary>
    /// <param name="claimed">The item as it was claimed.</param>
    /// <param name="reason">Why the item can never succeed.</param>
    /// <param name="update">Changes to apply to the stored item in the same save; optional.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the claim was still held and is settled; <see langword="false"/> when it was lost.</returns>
    Task<bool> AbandonAsync(T claimed, string reason, Action<T> update = null, CancellationToken cancellationToken = default);
}
