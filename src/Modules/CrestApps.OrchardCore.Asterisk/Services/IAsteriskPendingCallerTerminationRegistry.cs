namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Owns the process-wide lifecycle of an inbound caller channel that is being terminated by the create-lock-timeout
/// fail-safe in <see cref="AsteriskInboundCallOfferBridge"/>: it holds both the <em>termination claim</em> that makes
/// <see cref="IAsteriskChannelTenantBindingStore.CreateAsync"/> refuse to route the caller while the hang up is in
/// progress, and the <em>pending-retry queue</em> of channels whose inline hang up failed and must be retried over
/// time by the periodic reconciliation sweep. Both facets share one lifecycle so the claim can never outlive the
/// mechanism that releases it. The state is process-wide and tenant-partitioned — matching the per-channel create
/// locks it coordinates with — because a stranded-caller hang up is a remote ARI operation that can still be in
/// flight across a shell reload: a per-shell fence would be dropped while the old generation's hang up, or a new
/// generation's routing of the same channel, is still in progress, letting a late hang up terminate a freshly routed
/// call. Keeping the claim process-wide keeps a new shell generation fenced until the termination actually completes,
/// and keeping the pending queue process-wide guarantees the live generation's reconciler can finish the hang up and
/// release the claim. A restart drops the state alongside the ARI WebSocket, and Asterisk's
/// Stasis-application-disconnect disposition then releases any residual channel, so nothing is leaked across a
/// restart either.
/// </summary>
internal interface IAsteriskPendingCallerTerminationRegistry
{
    /// <summary>
    /// Determines whether a termination claim is currently held for the supplied caller channel. A held claim means
    /// the caller is being hung up, so a binding must not be created for it.
    /// </summary>
    /// <param name="channelId">The Asterisk caller channel identifier to test.</param>
    /// <returns><see langword="true"/> when a termination claim is held; otherwise <see langword="false"/>.</returns>
    bool HasTerminationClaim(string channelId);

    /// <summary>
    /// Records a termination claim for the supplied caller channel so that a create for it is refused while the hang
    /// up is in progress. The call is idempotent; claiming a channel that is already claimed has no additional effect.
    /// The caller must plant the claim while holding the per-channel create serialization lock so the claim is
    /// mutually exclusive with a concurrent create.
    /// </summary>
    /// <param name="channelId">The Asterisk caller channel identifier being terminated.</param>
    void PlantTerminationClaim(string channelId);

    /// <summary>
    /// Removes the termination claim for the supplied caller channel once it has been terminated or confirmed gone,
    /// re-permitting a future create for that channel. The call is idempotent.
    /// </summary>
    /// <param name="channelId">The Asterisk caller channel identifier whose claim is released.</param>
    void RemoveTerminationClaim(string channelId);

    /// <summary>
    /// Records that the supplied inbound caller channel still needs to be hung up. The call is idempotent; queuing a
    /// channel that is already pending has no additional effect.
    /// </summary>
    /// <param name="channelId">The Asterisk caller channel identifier that must be terminated.</param>
    void Enqueue(string channelId);

    /// <summary>
    /// Removes the supplied channel from the pending set once it has been terminated or is confirmed gone.
    /// </summary>
    /// <param name="channelId">The Asterisk caller channel identifier that has been resolved.</param>
    void Resolve(string channelId);

    /// <summary>
    /// Gets a point-in-time snapshot of the caller channels that still need to be terminated. Draining the snapshot
    /// and resolving each entry lets the periodic reconciler retry the termination without holding a lock across the
    /// ARI calls.
    /// </summary>
    /// <returns>The channel identifiers pending termination; an empty collection when none are pending.</returns>
    IReadOnlyCollection<string> GetPending();
}
