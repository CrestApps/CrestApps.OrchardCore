using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Reconciles tenant-owned Asterisk inbound and agent-leg channel bindings against live ARI state. It runs both
/// when the realtime listener reconnects and on a periodic background sweep, so a durable
/// <see cref="AsteriskChannelBindingState.Terminating"/> record whose live cleanup did not complete, a channel
/// whose terminal event was missed, and a <see cref="AsteriskChannelBindingState.Pending"/> binding orphaned by a
/// connect that crashed mid-flight are all recovered without waiting for a WebSocket reconnect. Every cleanup is
/// idempotent (the ARI client treats already-gone bridges and channels as success) and the durable record is
/// removed last, so a transient ARI outage leaves the record for the next sweep instead of leaking a resource.
/// </summary>
internal sealed class AsteriskInboundReconciler : IAsteriskProviderStateReconciler
{
    // A healthy caller-to-agent connect promotes its Pending agent-leg binding to Connected (or compensates and
    // removes it) within seconds — bounded by the agent answer timeout plus a couple of bridging calls. A Pending
    // binding still present well beyond that window can only be the residue of a connect that crashed before it
    // could finalize or compensate, so reclaiming it cannot tear down an in-flight connect.
    private static readonly TimeSpan _pendingReclamationThreshold = TimeSpan.FromMinutes(5);

    private readonly IAsteriskChannelTenantBindingStore _bindingStore;
    private readonly IAsteriskAriClient _ariClient;
    private readonly IProviderVoiceEventSink _providerVoiceEventSink;
    private readonly IInboundVoiceInteractionProbe _interactionProbe;
    private readonly IClock _clock;
    private readonly ILogger<AsteriskInboundReconciler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsteriskInboundReconciler"/> class.
    /// </summary>
    /// <param name="bindingStore">The tenant-scoped Asterisk channel binding store.</param>
    /// <param name="ariClient">The tenant-scoped Asterisk ARI client.</param>
    /// <param name="providerVoiceEventSink">The provider-agnostic Contact Center voice event sink.</param>
    /// <param name="interactionProbe">The probe used to recover an aged offering leg that is still a routed call.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger instance.</param>
    public AsteriskInboundReconciler(
        IAsteriskChannelTenantBindingStore bindingStore,
        IAsteriskAriClient ariClient,
        IProviderVoiceEventSink providerVoiceEventSink,
        IInboundVoiceInteractionProbe interactionProbe,
        IClock clock,
        ILogger<AsteriskInboundReconciler> logger)
    {
        _bindingStore = bindingStore;
        _ariClient = ariClient;
        _providerVoiceEventSink = providerVoiceEventSink;
        _interactionProbe = interactionProbe;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task ReconcileAsync(string providerName, CancellationToken cancellationToken = default)
    {
        var bindings = await _bindingStore.GetAllAsync(cancellationToken);

        foreach (var binding in bindings)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                // Exactly one Asterisk listener runs per tenant (see AsteriskRealtimeVoiceTenantEvents) and it
                // attributes every inbound and agent-leg binding to the voice provider technical name, so an
                // exact match scopes the sweep to this tenant's Asterisk channels checked against the one
                // resolved ARI this client uses.
                if (!string.Equals(binding.ProviderName, providerName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // A Terminating binding is a durable teardown record whose live cleanup did not complete (a
                // transient ARI failure or a crash between claiming the binding and applying its ARI effects), or
                // one a live terminal event deliberately left behind because it claimed a provisioning leg whose
                // in-flight allocator had not yet quiesced. Complete the cleanup and retire it once no allocator
                // can still depend on it.
                if (binding.State == AsteriskChannelBindingState.Terminating)
                {
                    await ResolveClaimedBindingAsync(binding, cancellationToken);

                    continue;
                }

                // A provisioning leg (a Pending agent leg or an Offering inbound caller leg) is owned by its
                // in-flight allocator — the connect or offer flow that persisted it — until its lease ages out.
                // Never reclaim it early, regardless of channel liveness: the agent channel does not exist until the
                // originate lands and a caller can momentarily be between bridges, so a sweep that tore a
                // provisioning leg down on liveness alone could destroy resources the allocator is still creating or
                // strand a caller the allocator still owns. Only once the lease has elapsed is the allocator
                // provably gone and the leg safe to reclaim.
                if (binding.State.IsProvisioning())
                {
                    if (!HasProvisioningLeaseElapsed(binding))
                    {
                        continue;
                    }

                    // An aged Offering caller leg can be a real routed call whose promotion to Connected was lost to
                    // a crash between the durable interaction commit and the offer flow's deferred promote. Recover it
                    // forward instead of tearing down a live, routed caller: when an active Contact Center interaction
                    // still exists for the call, promote the leg to Connected (a no-op unless it is still Offering) and
                    // leave it. Only a leg with no active interaction is a genuinely never-routed offer safe to reclaim.
                    if (binding.State == AsteriskChannelBindingState.Offering &&
                        await _interactionProbe.HasActiveInteractionAsync(binding.ProviderName, binding.ProviderCallId, cancellationToken))
                    {
                        await _bindingStore.TryPromoteOfferingAsync(binding.ChannelId);

                        continue;
                    }
                }
                else if (await _ariClient.ChannelExistsAsync(binding.ChannelId, cancellationToken))
                {
                    // A live, fully connected call is healthy; leave it. Only a Connected binding whose channel is
                    // gone (a missed terminal event) falls through to be claimed and reconciled.
                    continue;
                }

                // The binding must be cleaned. Claim it durably so a live connect finalization racing on it loses
                // to the reconciler, then clean up its resources per the disposition the claim captured.
                var claim = await _bindingStore.TryBeginTeardownAsync(binding.ChannelId);

                if (claim is null)
                {
                    continue;
                }

                await ResolveClaimedBindingAsync(claim.Binding, cancellationToken);
            }
            catch (Exception ex)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                _logger.LogWarning(
                    OperationalLogRedactor.RedactException(ex),
                    "Asterisk inbound channel reconciliation failed for provider {ProviderName} call {CallId}; continuing the sweep.",
                    OperationalLogRedactor.Redact(binding.ProviderName, OperationalLogFieldKind.FreeText),
                    OperationalLogRedactor.Pseudonymize(binding.ProviderCallId, OperationalLogIdentifierCategory.Call));
            }
        }
    }

    private bool HasProvisioningLeaseElapsed(AsteriskChannelTenantBinding binding)
    {
        return _clock.UtcNow - binding.CreatedUtc >= _pendingReclamationThreshold;
    }

    private async Task ResolveClaimedBindingAsync(AsteriskChannelTenantBinding binding, CancellationToken cancellationToken)
    {
        // Apply the idempotent ARI cleanup and caller disposition for a claimed (Terminating) binding, then retire
        // the durable record — but only once no in-flight allocator can still depend on it. A provisioning-disposition
        // record (a Pending agent leg or an Offering caller leg a live terminal event claimed while its allocator was
        // still running) is kept until its allocator's lease has elapsed, so a resource the allocator creates a moment
        // after the claim is still swept by a later sweep, and a detached caller is still recovered, rather than the
        // record being deleted out from under an allocator that has not yet quiesced. A cleanup that could not fully
        // resolve the caller disposition (a still-alive detached caller that could not be re-parked) also keeps the
        // record so a later sweep retries instead of stranding the caller.
        var handled = await CleanupClaimedBindingAsync(binding, cancellationToken);

        if (!handled)
        {
            return;
        }

        var disposition = binding.PreTeardownState ?? AsteriskChannelBindingState.Connected;

        if (disposition.IsProvisioning() && !HasProvisioningLeaseElapsed(binding))
        {
            return;
        }

        await _bindingStore.RemoveByChannelIdAsync(binding.ChannelId);
    }

    private async Task<bool> CleanupClaimedBindingAsync(AsteriskChannelTenantBinding binding, CancellationToken cancellationToken)
    {
        // The disposition is read from the durable record (set atomically when the binding was claimed), never from
        // transient state, so a crashed teardown recovers correctly: a leg claimed while Connected was a live call
        // whose caller must be released, whereas a leg claimed while Pending was owned by the connect flow (which
        // reparks the caller for re-offer), and a caller leg claimed while Offering was an inbound offer that never
        // routed. A null disposition defaults to the safe Connected behavior.
        var disposition = binding.PreTeardownState ?? AsteriskChannelBindingState.Connected;

        bool handled;

        if (!string.IsNullOrWhiteSpace(binding.BridgeId))
        {
            handled = await CleanupAgentLegAsync(binding, disposition, cancellationToken);
        }
        else if (disposition == AsteriskChannelBindingState.Offering)
        {
            handled = await CleanupOfferingLegAsync(binding, cancellationToken);
        }
        else
        {
            await CleanupCallerLegAsync(binding, cancellationToken);
            handled = true;
        }

        // When the record could not yet be fully resolved (a detached caller that is still alive but could not be
        // returned to holding), leave it in place without logging completion so the next sweep retries.
        if (handled && _logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Reconciled an ended Asterisk channel for provider {ProviderName} call {CallId} and released its resources.",
                OperationalLogRedactor.Redact(binding.ProviderName, OperationalLogFieldKind.FreeText),
                OperationalLogRedactor.Pseudonymize(binding.ProviderCallId, OperationalLogIdentifierCategory.Call));
        }

        return handled;
    }

    private async Task<bool> CleanupOfferingLegAsync(AsteriskChannelTenantBinding binding, CancellationToken cancellationToken)
    {
        // An inbound caller leg claimed while still Offering was answered and parked but never routed to a Contact
        // Center interaction — the offer flow crashed or was torn down before it could promote the leg to Connected.
        // There is no interaction to resume, so destroy the caller's derived holding bridge and gracefully hang up a
        // caller that is still alive rather than leave it parked in silence forever. Both effects are idempotent, so
        // re-running them on a later sweep before the record is retired is safe.
        await DestroyBridgeAsync(AsteriskConstants.HoldingBridgePrefix + binding.ChannelId, cancellationToken);

        if (await _ariClient.ChannelExistsAsync(binding.ChannelId, cancellationToken))
        {
            await HangupAsync(binding.ChannelId, cancellationToken);
        }

        return true;
    }

    private async Task<bool> CleanupAgentLegAsync(
        AsteriskChannelTenantBinding binding,
        AsteriskChannelBindingState disposition,
        CancellationToken cancellationToken)
    {
        // Destroy the shared mixing bridge and hang up the agent channel itself (idempotent; covers the case where
        // a connect crashed while the agent channel was still live in Stasis).
        await DestroyBridgeAsync(binding.BridgeId, cancellationToken);
        await HangupAsync(binding.ChannelId, cancellationToken);

        if (disposition == AsteriskChannelBindingState.Connected &&
            !string.IsNullOrWhiteSpace(binding.PeerChannelId))
        {
            // A live, fully connected call: release the caller leg and emit the ended projection.
            await ReleaseCallerPeerAsync(binding.PeerChannelId, cancellationToken);
            await EmitEndedAsync(binding, cancellationToken);
        }
        else if (binding.CallerDetached && !string.IsNullOrWhiteSpace(binding.PeerChannelId))
        {
            // A connect that had already pulled the caller out of holding then failed or crashed before finalizing.
            // The caller is alive with no bridge, so return it to holding for re-offer BEFORE retiring the record.
            // If it cannot be re-parked yet (a transient ARI failure while the caller is still alive), keep the
            // durable record so a later sweep retries instead of stranding the caller in silence.
            if (!await TryReturnDetachedCallerToHoldingAsync(binding.PeerChannelId, cancellationToken))
            {
                return false;
            }
        }

        // The record itself is retired by ResolveClaimedBindingAsync only once no in-flight allocator can still
        // depend on it (provisioning records are kept until their lease elapses). Returning true signals the ARI
        // cleanup and caller disposition are fully resolved so retirement may proceed when the lease allows.
        return true;
    }

    private async Task<bool> TryReturnDetachedCallerToHoldingAsync(string callerChannelId, CancellationToken cancellationToken)
    {
        // Only re-park a caller that is still live. A caller that has since hung up needs nothing — its own inbound
        // binding is retired by its terminal event (or a later sweep) — so treat it as handled.
        if (!await _ariClient.ChannelExistsAsync(callerChannelId, cancellationToken))
        {
            return true;
        }

        var holdingBridgeId = AsteriskConstants.HoldingBridgePrefix + callerChannelId;

        try
        {
            await _ariClient.CreateBridgeAsync(holdingBridgeId, AsteriskAriConstants.HoldingBridgeType, cancellationToken);
            await _ariClient.AddChannelToBridgeAsync(holdingBridgeId, callerChannelId, cancellationToken);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Reconciliation returned a detached Asterisk caller {CallerChannelId} to holding after a crashed agent connect so the work can be re-offered.",
                    OperationalLogRedactor.Pseudonymize(callerChannelId, OperationalLogIdentifierCategory.Call));
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                OperationalLogRedactor.RedactException(ex),
                "Reconciliation could not return detached Asterisk caller {CallerChannelId} to holding; retaining the durable record to retry on a later sweep.",
                OperationalLogRedactor.Pseudonymize(callerChannelId, OperationalLogIdentifierCategory.Call));

            return false;
        }
    }

    private async Task CleanupCallerLegAsync(AsteriskChannelTenantBinding binding, CancellationToken cancellationToken)
    {
        // Destroy the caller's holding bridge, then release EVERY agent generation still referencing this caller.
        // A re-offer after a failed connect can leave more than one binding referencing the caller (a stale
        // Terminating agent generation and the live one), so claiming and tearing down every generation guarantees
        // a live agent leg and its mixing bridge are never left stranded behind a stale generation when the caller's
        // own terminal event was the one missed.
        await DestroyBridgeAsync(AsteriskConstants.HoldingBridgePrefix + binding.ChannelId, cancellationToken);

        var peerBindings = await _bindingStore.FindAllByPeerChannelIdAsync(binding.ChannelId);

        foreach (var peerBinding in peerBindings)
        {
            var peerClaim = await _bindingStore.TryBeginTeardownAsync(peerBinding.ChannelId);

            if (peerClaim is null)
            {
                continue;
            }

            await DestroyBridgeAsync(peerClaim.Binding.BridgeId, cancellationToken);
            await HangupAsync(peerClaim.Binding.ChannelId, cancellationToken);

            // A peer agent generation claimed while still Pending is kept so its in-flight connect flow's late
            // resources are swept by a later sweep before the record is age-retired, mirroring the primary-leg fence.
            if (!peerClaim.PreviousState.IsProvisioning())
            {
                await _bindingStore.RemoveByChannelIdAsync(peerClaim.Binding.ChannelId);
            }
        }

        await HangupAsync(binding.ChannelId, cancellationToken);
        await EmitEndedAsync(binding, cancellationToken);
    }

    private async Task ReleaseCallerPeerAsync(string callerChannelId, CancellationToken cancellationToken)
    {
        // Claim the caller-leg binding with the same durable compare-and-set so a concurrent caller terminal event
        // cannot race this teardown, then destroy the caller's holding bridge and hang it up. A null claim means the
        // caller leg is already gone or owned by another terminal event, so its binding retirement is left to that
        // owner; the holding-bridge destroy and hangup remain safe because the ARI client treats already-gone
        // resources as success.
        var callerClaim = await _bindingStore.TryBeginTeardownAsync(callerChannelId);

        await DestroyBridgeAsync(AsteriskConstants.HoldingBridgePrefix + callerChannelId, cancellationToken);
        await HangupAsync(callerChannelId, cancellationToken);

        if (callerClaim is not null)
        {
            await _bindingStore.RemoveByChannelIdAsync(callerChannelId);
        }
    }

    private async Task EmitEndedAsync(AsteriskChannelTenantBinding binding, CancellationToken cancellationToken)
    {
        // Both legs of a call share the caller's provider call id, so keying the recovered Ended by it collapses a
        // caller-leg and an agent-leg recovery of the same call to a single downstream event.
        await _providerVoiceEventSink.IngestAsync(new ProviderVoiceEvent
        {
            ProviderName = binding.ProviderName,
            ProviderCallId = binding.ProviderCallId,
            State = ContactCenterCallState.Ended,
            OccurredUtc = _clock.UtcNow,
            IdempotencyKey = "asterisk-reconcile-hangup-" + binding.ProviderCallId,
        }, cancellationToken);
    }

    private async Task DestroyBridgeAsync(string bridgeId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(bridgeId))
        {
            return;
        }

        await _ariClient.DestroyBridgeAsync(bridgeId, cancellationToken);
    }

    private async Task HangupAsync(string channelId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(channelId))
        {
            return;
        }

        await _ariClient.HangupAsync(channelId, cancellationToken);
    }
}
