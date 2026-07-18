using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Releases Asterisk call resources when a channel reaches a terminal state so normal call completion does not
/// leak mixing bridges, holding bridges, or channel ownership bindings. It reacts to <c>StasisEnd</c> and
/// <c>ChannelDestroyed</c> events, both of which can fire for the same channel. Terminal processing is
/// linearized against the connect flow's pending-to-connected finalization by a single durable compare-and-set:
/// claiming a binding transitions it to <see cref="AsteriskChannelBindingState.Terminating"/> using YesSql
/// document-version optimistic concurrency, so whichever side commits first owns the call's disposition. Because
/// the claim is the linearization point — not an external lock — a terminal event can never act on a stale
/// snapshot, and the second terminal event for a channel is a no-op once the first has claimed it. Every ARI
/// effect runs after the claim; if an effect genuinely fails, the durable <see cref="AsteriskChannelBindingState.Terminating"/>
/// record is deliberately left in place so the reconciler retries the cleanup rather than leaking an orphaned
/// resource. All state is read from and written to the current tenant's binding store and ARI client, keeping
/// teardown tenant-isolated.
/// </summary>
internal sealed class AsteriskCallTeardownService : IAsteriskCallTeardownService
{
    private readonly IAsteriskChannelTenantBindingStore _bindingStore;
    private readonly IAsteriskAriClient _ariClient;
    private readonly ILogger<AsteriskCallTeardownService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsteriskCallTeardownService"/> class.
    /// </summary>
    /// <param name="bindingStore">The tenant-scoped channel ownership binding store.</param>
    /// <param name="ariClient">The tenant-scoped Asterisk ARI client.</param>
    /// <param name="logger">The logger instance.</param>
    public AsteriskCallTeardownService(
        IAsteriskChannelTenantBindingStore bindingStore,
        IAsteriskAriClient ariClient,
        ILogger<AsteriskCallTeardownService> logger)
    {
        _bindingStore = bindingStore;
        _ariClient = ariClient;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task ReleaseAsync(AsteriskRealtimeVoiceEvent voiceEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(voiceEvent);

        if (!IsTerminalEvent(voiceEvent.EventType) ||
            string.IsNullOrWhiteSpace(voiceEvent.ChannelId))
        {
            return;
        }

        try
        {
            // The claim is the durable linearization point. It atomically transitions the binding to Terminating,
            // so a concurrent connect finalization can no longer promote it and a second terminal event for the same
            // channel gets nothing to claim. A null claim means the call is already cleaned up (or owned by another
            // terminal event), so there is nothing to do. The claim runs INSIDE this boundary so a store or DB
            // failure never escapes into the dispatcher's finally — it leaves no durable record but the reconciler's
            // periodic sweep still recovers any resource that a later re-read observes as orphaned.
            var claim = await _bindingStore.TryBeginTeardownAsync(voiceEvent.ChannelId);

            if (claim is null)
            {
                return;
            }

            var plan = await BuildPlanAsync(claim);
            await ExecutePlanAsync(plan);
            await RetireClaimAsync(plan);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Released Asterisk call resources for terminal event {EventType} on channel {ChannelId}.",
                    OperationalLogRedactor.Redact(voiceEvent.EventType, OperationalLogFieldKind.FreeText),
                    OperationalLogRedactor.Pseudonymize(voiceEvent.ChannelId, OperationalLogIdentifierCategory.Call));
            }
        }
        catch (Exception ex)
        {
            // A binding may have been durably claimed for teardown but an ARI (or store) effect genuinely failed.
            // Leave any Terminating record(s) in place — the periodic reconciler re-runs the cleanup, so a transient
            // failure or crash never leaks an orphaned bridge with no owner. Teardown runs inside the dispatcher's
            // finally, so it must never surface the failure to the caller.
            _logger.LogWarning(
                OperationalLogRedactor.RedactException(ex),
                "Asterisk terminal cleanup for channel {ChannelId} did not complete; leaving a durable teardown record for the reconciler to retry.",
                OperationalLogRedactor.Pseudonymize(voiceEvent.ChannelId, OperationalLogIdentifierCategory.Call));
        }
    }

    private async Task<TeardownPlan> BuildPlanAsync(AsteriskChannelTeardownClaim claim)
    {
        var binding = claim.Binding;

        if (!string.IsNullOrWhiteSpace(binding.BridgeId))
        {
            // Agent leg: it owns the shared mixing bridge and references the caller as its peer. Release the caller
            // only when the leg was already Connected (a live call whose agent dropped). While the leg was still
            // Pending the connect flow owns the caller's disposition: our claim makes its MarkConnectedAsync lose,
            // so it compensates and reparks the caller for re-offer — releasing the caller here would hang up a
            // caller the connect flow is about to re-queue.
            //
            // For a Connected call, claim the caller (peer) leg with the same durable compare-and-set and retire it
            // here, mirroring the caller-leg path. This keeps teardown self-contained: the caller is hung up AND its
            // durable binding is claimed and removed as part of this event, so the system never depends on the
            // caller's own terminal event arriving. A null caller claim means a concurrent caller-leg terminal event
            // already owns it, so we neither hang it up nor retire it and leave both to that owner.
            var agentPeers = new List<PeerLegTeardown>();

            if (claim.PreviousState == AsteriskChannelBindingState.Connected &&
                !string.IsNullOrWhiteSpace(binding.PeerChannelId))
            {
                var callerClaim = await _bindingStore.TryBeginTeardownAsync(binding.PeerChannelId);

                if (callerClaim is not null)
                {
                    agentPeers.Add(new PeerLegTeardown
                    {
                        ChannelId = callerClaim.Binding.ChannelId,
                        DestroyHoldingBridge = true,
                        RetainRecord = callerClaim.PreviousState.IsProvisioning(),
                    });
                }
            }

            return new TeardownPlan
            {
                BindingChannelId = binding.ChannelId,
                IsAgentLeg = true,
                MixingBridgeId = binding.BridgeId,
                RetainRecord = claim.PreviousState.IsProvisioning(),
                PeerLegs = agentPeers,
            };
        }

        // Caller leg: tear down its holding bridge (covers the never-connected case) and, if it was connected to an
        // agent, release EVERY agent generation discovered through the indexed peer reverse lookup. A re-offer after
        // a failed attempt can leave more than one binding referencing this caller (a stale Terminating agent
        // generation and the live one), so claiming every generation — rather than an arbitrary single match —
        // guarantees the live agent leg and its mixing bridge are never left stranded behind a stale generation.
        // Each agent generation is claimed with the same durable compare-and-set so a connect finalization racing on
        // it loses to this teardown; a null claim means that generation is already gone or owned by another terminal
        // event, so its resources are left to that owner.
        var callerPeers = new List<PeerLegTeardown>();
        var peerBindings = await _bindingStore.FindAllByPeerChannelIdAsync(binding.ChannelId);

        foreach (var peerBinding in peerBindings)
        {
            var peerClaim = await _bindingStore.TryBeginTeardownAsync(peerBinding.ChannelId);

            if (peerClaim is not null)
            {
                callerPeers.Add(new PeerLegTeardown
                {
                    ChannelId = peerClaim.Binding.ChannelId,
                    MixingBridgeId = peerClaim.Binding.BridgeId,
                    RetainRecord = peerClaim.PreviousState.IsProvisioning(),
                });
            }
        }

        return new TeardownPlan
        {
            BindingChannelId = binding.ChannelId,
            IsAgentLeg = false,
            CallerLegChannelId = binding.ChannelId,
            RetainRecord = claim.PreviousState.IsProvisioning(),
            PeerLegs = callerPeers,
        };
    }

    private async Task ExecutePlanAsync(TeardownPlan plan)
    {
        if (plan.IsAgentLeg)
        {
            await DestroyBridgeAsync(plan.MixingBridgeId);
        }
        else
        {
            await DestroyBridgeAsync(AsteriskConstants.HoldingBridgePrefix + plan.CallerLegChannelId);
        }

        foreach (var peer in plan.PeerLegs)
        {
            await DestroyBridgeAsync(peer.MixingBridgeId);

            if (peer.DestroyHoldingBridge)
            {
                await DestroyBridgeAsync(AsteriskConstants.HoldingBridgePrefix + peer.ChannelId);
            }

            await HangupAsync(peer.ChannelId);
        }
    }

    private async Task RetireClaimAsync(TeardownPlan plan)
    {
        // Every ARI effect for the plan has been applied, so the durable Terminating records can be retired. This
        // removal is best-effort: a residual Terminating record is harmless because the reconciler re-runs the
        // (idempotent) cleanup and removes it, so a failed removal here never leaks a resource.
        //
        // A binding claimed while still in a provisioning phase (Pending agent leg or Offering caller leg) is NOT
        // retired here: its allocator (the connect or offer flow) may still be creating deterministic resources or
        // owe caller re-parking, so its Terminating record must survive for the reconciler to idempotently finish
        // and age-retire once the allocator's lease has elapsed. Retiring it now could delete the only record that
        // tracks a resource the allocator creates a moment later, or the marker that drives re-parking a detached
        // caller — the exact leak/strand this fence closes.
        if (!plan.RetainRecord)
        {
            await RemoveBindingAsync(plan.BindingChannelId);
        }

        foreach (var peer in plan.PeerLegs)
        {
            if (!peer.RetainRecord)
            {
                await RemoveBindingAsync(peer.ChannelId);
            }
        }
    }

    private static bool IsTerminalEvent(string eventType)
    {
        return string.Equals(eventType, "ChannelDestroyed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(eventType, "StasisEnd", StringComparison.OrdinalIgnoreCase);
    }

    private async Task DestroyBridgeAsync(string bridgeId)
    {
        if (string.IsNullOrWhiteSpace(bridgeId))
        {
            return;
        }

        // The ARI client treats an already-gone bridge (404) as success, so this throws only on a genuine failure.
        // The exception propagates so ReleaseAsync leaves the durable Terminating record for the reconciler.
        await _ariClient.DestroyBridgeAsync(bridgeId, CancellationToken.None);
    }

    private async Task HangupAsync(string channelId)
    {
        if (string.IsNullOrWhiteSpace(channelId))
        {
            return;
        }

        // The ARI client treats an already-gone channel (404) as success, so this throws only on a genuine failure,
        // which propagates so ReleaseAsync leaves the durable Terminating record for the reconciler.
        await _ariClient.HangupAsync(channelId, CancellationToken.None);
    }

    private async Task RemoveBindingAsync(string channelId)
    {
        try
        {
            await _bindingStore.RemoveByChannelIdAsync(channelId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(OperationalLogRedactor.RedactException(ex), "Asterisk terminal binding cleanup did not complete cleanly; the reconciler will retire the record.");
        }
    }

    private sealed class TeardownPlan
    {
        public string BindingChannelId { get; init; }

        public bool IsAgentLeg { get; init; }

        public string MixingBridgeId { get; init; }

        public string CallerLegChannelId { get; init; }

        public bool RetainRecord { get; init; }

        public IReadOnlyList<PeerLegTeardown> PeerLegs { get; init; } = [];
    }

    private sealed class PeerLegTeardown
    {
        public string ChannelId { get; init; }

        public string MixingBridgeId { get; init; }

        public bool DestroyHoldingBridge { get; init; }

        public bool RetainRecord { get; init; }
    }
}
