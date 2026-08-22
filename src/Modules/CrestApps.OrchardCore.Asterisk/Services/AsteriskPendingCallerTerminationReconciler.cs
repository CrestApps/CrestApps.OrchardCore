using CrestApps.Core.Support;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Drains the <see cref="IAsteriskPendingCallerTerminationRegistry"/> on every reconciliation sweep, completing the
/// termination of each inbound caller channel that could not be hung up inline after a create-lock timeout. Because
/// the create-lock timeout leaves no durable binding, the binding-scoped <see cref="AsteriskInboundReconciler"/>
/// cannot recover these callers; this reconciler is the "recover over time" path the fail-safe relies on when a
/// transient ARI outage outlasts the offer bridge's bounded inline retries, and it is also the path that completes a
/// termination whose inline attempt was abandoned by a shell reload. For each pending channel it re-affirms the
/// termination claim through <see cref="IAsteriskChannelTenantBindingStore.TryClaimChannelForTerminationAsync"/>: a
/// channel a concurrent create has since legitimately recovered (a binding now exists) is dropped without a hang up,
/// a channel whose create lock is momentarily wedged is left for the next sweep, and an affirmed claim keeps
/// <see cref="IAsteriskChannelTenantBindingStore.CreateAsync"/> fenced so no delivery can route the caller while the
/// hang up runs outside any lock. The hang up is idempotent (the ARI client maps an already-gone channel to success),
/// so a channel is resolved (and its claim released) on the first sweep during which ARI is reachable and remains
/// queued only while the transport error persists.
/// </summary>
internal sealed class AsteriskPendingCallerTerminationReconciler : IAsteriskProviderStateReconciler
{
    private readonly IAsteriskPendingCallerTerminationRegistry _registry;
    private readonly IAsteriskChannelTenantBindingStore _bindingStore;
    private readonly IAsteriskAriClient _ariClient;
    private readonly ILogger<AsteriskPendingCallerTerminationReconciler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsteriskPendingCallerTerminationReconciler"/> class.
    /// </summary>
    /// <param name="registry">The pending caller-termination registry to drain.</param>
    /// <param name="bindingStore">The tenant-scoped channel binding store used to re-affirm and release a termination claim around the hang up.</param>
    /// <param name="ariClient">The tenant-scoped Asterisk ARI client.</param>
    /// <param name="logger">The logger instance.</param>
    public AsteriskPendingCallerTerminationReconciler(
        IAsteriskPendingCallerTerminationRegistry registry,
        IAsteriskChannelTenantBindingStore bindingStore,
        IAsteriskAriClient ariClient,
        ILogger<AsteriskPendingCallerTerminationReconciler> logger)
    {
        _registry = registry;
        _bindingStore = bindingStore;
        _ariClient = ariClient;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task ReconcileAsync(string providerName, CancellationToken cancellationToken = default)
    {
        var pending = _registry.GetPending();

        if (pending.Count == 0)
        {
            return;
        }

        foreach (var channelId in pending)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            bool claimed;

            try
            {
                // Re-affirm the claim under the per-channel create lock before hanging up. This both re-plants a claim
                // that was only enqueued (the create-lock-timeout path could not plant one inline) and re-checks that
                // no concurrent create has legitimately recovered the caller in the meantime. Because the claim is
                // process-wide, an affirmed claim keeps CreateAsync fenced across shell generations for the duration
                // of the hang up, so a late or reload-abandoned attempt can never terminate a freshly routed call.
                claimed = await _bindingStore.TryClaimChannelForTerminationAsync(channelId, cancellationToken);
            }
            catch (AsteriskChannelBindingCreateTimeoutException)
            {
                // The create lock is momentarily wedged by another create on this stripe; leave the channel pending
                // and retry on the next sweep once the lock frees.
                continue;
            }

            if (!claimed)
            {
                // A binding now exists: a concurrent create legitimately recovered this caller into a live, owned
                // call. Drop it from the pending set and never hang it up.
                _registry.Resolve(channelId);

                continue;
            }

            try
            {
                // The claim is affirmed, so CreateAsync refuses to route the caller: there is no live call to protect
                // and no lock is held across the hang up. The ARI client maps a 404 (already-gone channel) to success,
                // so a completed call means the caller is genuinely released; release the claim and drop the channel
                // from the pending set. A thrown transport error leaves the claim and the channel in place so the
                // retry continues on the next sweep until ARI is reachable again.
                await _ariClient.HangupAsync(channelId, cancellationToken);
                _bindingStore.ReleaseTerminationClaim(channelId);
                _registry.Resolve(channelId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Asterisk could not hang up pending inbound caller channel {ChannelId} during the reconciliation sweep; it will be retried on the next sweep.",
                    channelId.SanitizeLogValue());
            }
        }
    }
}
