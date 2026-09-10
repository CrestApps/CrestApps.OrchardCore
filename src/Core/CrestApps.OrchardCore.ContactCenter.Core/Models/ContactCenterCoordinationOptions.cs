namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// The distributed-lock timings the Contact Center coordinates inbound work with. These are deployment
/// characteristics rather than product constants: a node under heavier load, or one further from its database,
/// needs a longer lease before a peer may assume the holder died.
/// </summary>
public sealed class ContactCenterCoordinationOptions
{
    /// <summary>
    /// Gets or sets how long a node waits to acquire the inbound routing lock for a call before giving up. A
    /// caller that gives up does not route the call twice; it defers to the node that holds the lock.
    /// </summary>
    public TimeSpan InboundLockTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets how long the inbound routing lock is held before it expires on its own, which bounds how
    /// long a crashed node blocks routing for the same call.
    /// </summary>
    public TimeSpan InboundLockExpiration { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets how long a node waits to acquire a queue's assignment lock before giving up and leaving the
    /// item for the node that holds it.
    /// </summary>
    public TimeSpan AssignmentLockTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets how long the assignment lock is held before it expires, bounding how long a crashed node
    /// blocks assignment for a queue.
    /// </summary>
    public TimeSpan AssignmentLockExpiration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets how long a node waits for a reservation lock before giving up.
    /// </summary>
    public TimeSpan ReservationLockTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets how long a reservation lock is held before it expires.
    /// </summary>
    public TimeSpan ReservationLockExpiration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the short wait the latency-sensitive reclaim path uses.
    /// </summary>
    /// <remarks>
    /// It must stay positive: the distributed Redis lock provider gates its acquisition loop on a
    /// timeout-derived cancellation token, so a zero wait cancels before the first attempt and the lock is never
    /// tried at all on a Redis-backed tenant. Keeping it below the provider's first retry back-off makes it
    /// effectively one attempt on both providers.
    /// </remarks>
    public TimeSpan ReclaimLockWait { get; set; } = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// Gets or sets how many expired reservations one drain page materializes, so a large expiry backlog is
    /// processed in bounded batches rather than loaded at once.
    /// </summary>
    public int ExpiryPageSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets how long a queued-work sync lease is held for one agent, which is what debounces a client
    /// that asks for its queued work more often than the work can change.
    /// </summary>
    public TimeSpan QueuedWorkSyncLease { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Gets or sets how often a client falls back to asking for its queued work when no event has prompted it.
    /// </summary>
    public TimeSpan QueuedWorkSyncFallbackInterval { get; set; } = TimeSpan.FromSeconds(30);
}
