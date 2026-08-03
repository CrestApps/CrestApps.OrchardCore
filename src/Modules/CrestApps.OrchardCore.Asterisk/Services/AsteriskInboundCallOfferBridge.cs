using CrestApps.Core.Support;
using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Bridges first-seen inbound Asterisk caller channels into the Contact Center inbound voice pipeline.
/// </summary>
internal sealed class AsteriskInboundCallOfferBridge : IAsteriskRealtimeVoiceEventBridge
{
    // A create-lock timeout leaves no durable binding, so the binding-scoped reconciler cannot retry the caller's
    // termination. Retry the hang up a small, bounded number of times so a single transient ARI failure does not
    // strand the live caller; a persistent failure is escalated to an error for out-of-band reconciliation.
    private const int StrandedCallerHangupAttempts = 3;

    private readonly IAsteriskChannelTenantBindingStore _bindingStore;
    private readonly IAsteriskAriClient _ariClient;
    private readonly IInboundVoiceEventSink _inboundVoiceEventSink;
    private readonly IAsteriskPendingCallerTerminationRegistry _pendingCallerTerminationRegistry;
    private readonly IClock _clock;
    private readonly ILogger<AsteriskInboundCallOfferBridge> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsteriskInboundCallOfferBridge"/> class.
    /// </summary>
    /// <param name="bindingStore">The tenant-scoped channel binding store.</param>
    /// <param name="ariClient">The tenant-scoped Asterisk ARI client.</param>
    /// <param name="inboundVoiceEventSink">The Contact Center inbound voice event sink.</param>
    /// <param name="pendingCallerTerminationRegistry">The registry that tracks callers awaiting a retried hang up.</param>
    /// <param name="clock">The clock used to stamp tenant-owned state.</param>
    /// <param name="logger">The logger instance.</param>
    public AsteriskInboundCallOfferBridge(
        IAsteriskChannelTenantBindingStore bindingStore,
        IAsteriskAriClient ariClient,
        IInboundVoiceEventSink inboundVoiceEventSink,
        IAsteriskPendingCallerTerminationRegistry pendingCallerTerminationRegistry,
        IClock clock,
        ILogger<AsteriskInboundCallOfferBridge> logger)
    {
        _bindingStore = bindingStore;
        _ariClient = ariClient;
        _inboundVoiceEventSink = inboundVoiceEventSink;
        _pendingCallerTerminationRegistry = pendingCallerTerminationRegistry;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Attempts to answer, park, bind, and route a first-seen inbound Asterisk caller channel.
    /// </summary>
    /// <param name="voiceEvent">The normalized Asterisk voice event.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the event was handled; otherwise, <see langword="false"/>.</returns>
    public async Task<bool> TryHandleAsync(
        AsteriskRealtimeVoiceEvent voiceEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(voiceEvent);

        if (!voiceEvent.IsInbound)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(voiceEvent.ChannelId))
        {
            return false;
        }

        var existing = await _bindingStore.FindByChannelIdAsync(voiceEvent.ChannelId, cancellationToken);

        if (existing is not null)
        {
            return true;
        }

        var holdingBridgeId = AsteriskConstants.HoldingBridgePrefix + voiceEvent.ChannelId;
        var bridgeCreateAttempted = false;
        var answerAttempted = false;

        // Persist the durable caller binding BEFORE any ARI side effect. It is both the inbound channel's
        // idempotency claim (a duplicate StasisStart loses the atomic create below and short-circuits) and the
        // recovery record for every resource this offer creates: a crash after answering or parking — not only a
        // thrown exception — then always leaves a binding that the terminal-event teardown and the periodic reconciler
        // can use to release the holding bridge and the caller. The binding names no bridge (it is the caller leg), so
        // cleanup derives the holding bridge id deterministically from the channel id. It is written Offering — a
        // provisioning phase the offer flow still owns — so a terminal event that claims it does not treat the
        // caller as a live connected call, and the reconciler does not treat a still-alive offering leg as healthy:
        // a crash before routing completes leaves a record the reconciler resolves (terminating an aged, never-routed
        // caller) instead of a caller stranded in silence. It is promoted to Connected only once routing succeeds.
        bool created;

        try
        {
            created = await _bindingStore.CreateAsync(new AsteriskChannelTenantBinding
            {
                ChannelId = voiceEvent.ChannelId,
                ProviderName = voiceEvent.ProviderName,
                InteractionId = voiceEvent.InteractionCorrelationId,
                ProviderCallId = voiceEvent.CallId,
                State = AsteriskChannelBindingState.Offering,
                CreatedUtc = _clock.UtcNow,
            });
        }
        catch (AsteriskChannelBindingCreateTimeoutException ex)
        {
            // The per-channel create-serialization lock could not be acquired within its bounded window: another
            // create on this stripe — a wedged same-channel delivery, or an unrelated colliding channel — held it
            // too long. No binding was persisted, and the reconciler sweeps only existing bindings, so this caller,
            // already live in the Stasis application, would otherwise sit unanswered and untracked indefinitely.
            // Fail safe by hanging the caller up deterministically rather than stranding it in silence. A duplicate
            // same-channel delivery whose peer is legitimately mid-setup only reaches this path after the bounded
            // window has already elapsed with the caller unanswered, and the wedged peer's own answer and bridge
            // calls then compensate against a gone channel.
            _logger.LogWarning(
                ex,
                "Timed out acquiring the create-serialization lock for inbound call {CallId}; terminating the caller to avoid stranding it.",
                voiceEvent.CallId.SanitizeLogValue());

            // Because no durable binding exists, the binding-scoped reconciler cannot retry this cleanup, so the
            // termination cannot rely on a single best-effort call: a transient ARI failure would leave the live
            // caller stranded with nothing tracking it. Track the channel in the process-wide pending-termination
            // registry BEFORE attempting the hang up, so that even if this scope is torn down mid-attempt by a shell
            // reload the reconciler still owns the channel and completes — or releases — the termination. Then retry
            // the hang up a bounded number of times inline (a genuine transport failure is distinct from an
            // already-gone channel, which the ARI client reports as success); on inline success the terminator
            // resolves the channel back out. When every inline attempt fails — a transport error can persist across a
            // brief circuit-breaker window while the WebSocket stays connected, so Asterisk's Stasis-disconnect
            // disposition is not guaranteed to fire — the channel stays queued so the periodic reconciliation sweep
            // keeps retrying the hang up over time, and an error naming the channel keeps the residual live caller
            // operator-visible meanwhile.
            _pendingCallerTerminationRegistry.Enqueue(voiceEvent.ChannelId);

            if (!await TryTerminateStrandedCallerAsync(voiceEvent))
            {
                _logger.LogError(
                    "Asterisk could not hang up inbound caller {CallId} inline after a create-lock timeout; it remains queued for retried termination by the reconciliation sweep.",
                    voiceEvent.CallId.SanitizeLogValue());
            }

            return true;
        }

        if (!created)
        {
            // Another delivery of this StasisStart already claimed the channel and owns its offer flow, OR a
            // stranded-caller fail-safe termination has claimed the channel and the caller is being hung up. Either
            // way the caller that loses the create must not answer, create the holding bridge, park, or route the
            // caller: the winning owner (or the termination) is authoritative.
            return true;
        }

        try
        {
            // Mark the answer as ATTEMPTED before awaiting it. If Asterisk answers the caller but the response is
            // lost (a dropped ack, or a crash between the server-side answer and the await returning), the caller is
            // live server-side, so the failure path must treat an attempted answer as possibly-answered and hang the
            // caller up rather than skip its teardown and strand an answered caller in silence.
            answerAttempted = true;
            await _ariClient.AnswerAsync(voiceEvent.ChannelId, cancellationToken);

            // Mark the create as ATTEMPTED before awaiting it. If Asterisk creates the holding bridge but the
            // response is lost (a dropped ack, or a crash between the server-side create and the await returning),
            // the deterministic bridge id can still be live, so the failure path must treat an attempted create as
            // possibly-orphaned and compensate it rather than skip its teardown.
            bridgeCreateAttempted = true;
            await _ariClient.CreateBridgeAsync(holdingBridgeId, AsteriskAriConstants.HoldingBridgeType, cancellationToken);

            await _ariClient.AddChannelToBridgeAsync(holdingBridgeId, voiceEvent.ChannelId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Answering, creating the holding bridge, or parking the caller failed. Terminate the offer — but when the
            // failure was transport-ambiguous (a client timeout, or a transport error that returned no server
            // response) the provisioning call may still commit on Asterisk after this sweep, so retain the durable
            // record for the reconciler instead of deleting it on a compensation that "succeeds" only because the
            // resource is not there yet.
            var provisioningOutcomeAmbiguous = AsteriskAriOutcomeClassifier.IsProvisioningOutcomeAmbiguous(ex);

            _logger.LogError(
                ex,
                "Asterisk failed to offer inbound call {CallId}; terminating the caller.",
                voiceEvent.CallId.SanitizeLogValue());

            await TerminateOfferAsync(voiceEvent, holdingBridgeId, answerAttempted, bridgeCreateAttempted, provisioningOutcomeAmbiguous);

            return true;
        }

        InboundVoiceRouteOutcome outcome;

        try
        {
            var metadata = voiceEvent.Metadata is null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(voiceEvent.Metadata, StringComparer.OrdinalIgnoreCase);

            outcome = await _inboundVoiceEventSink.RouteAsync(new InboundVoiceEvent
            {
                ProviderName = voiceEvent.ProviderName,
                ProviderCallId = voiceEvent.CallId,
                FromAddress = voiceEvent.CallerNumber ?? voiceEvent.FromAddress,
                ToAddress = voiceEvent.DialedNumber ?? voiceEvent.ToAddress,
                CallerName = voiceEvent.CallerNumber,
                ReceivedUtc = voiceEvent.OccurredUtc ?? _clock.UtcNow,
                Metadata = metadata,
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Routing can durably commit the interaction and its queue item and THEN throw (for example, a post-commit
            // publish failure). Terminating here would hang up the caller and delete the Offering binding that ties
            // the still-parked caller to that committed interaction, orphaning an interaction the reconciler could no
            // longer recover. Retain the offer instead: leave the caller parked in the holding bridge and the binding
            // in Offering. The reconciler promotes an aged Offering leg that still has an active interaction (the
            // caller is then connected by the normal agent-connect flow) or terminates one that has none. Do NOT
            // promote here — the commit is not confirmed from this scope.
            _logger.LogError(
                ex,
                "Asterisk inbound routing failed after call {CallId} was parked; retaining the offer for reconciliation.",
                voiceEvent.CallId.SanitizeLogValue());

            return true;
        }

        if (outcome?.HasInteraction != true)
        {
            // Routing did not create a durable interaction (the tenant is quiescing or no service address is
            // configured), so there is nothing to connect the answered, parked caller to. Terminate the offer rather
            // than leave the caller in silence — and never promote it to Connected, which would falsely mark an
            // unrouted call healthy and hide it from the reconciler. Provisioning completed here, so the caller and
            // holding bridge definitely exist and can be compensated and removed normally.
            await TerminateOfferAsync(voiceEvent, holdingBridgeId, answerAttempted, bridgeCreateAttempted, provisioningOutcomeAmbiguous: false);

            return true;
        }

        // A durable interaction exists, so promote the caller leg out of the provisioning Offering phase to
        // Connected — but only AFTER the ambient scope that created the interaction commits, so an interaction that is
        // rolled back never leaves a caller falsely marked Connected. When there is no ambient scope (a direct
        // in-process invocation), the interaction is already durable, so promote inline. A crash between the
        // interaction commit and this deferred promote is recovered by the reconciler, which promotes an aged Offering
        // leg that still has an active interaction instead of tearing down a live, routed caller. TryPromoteOfferingAsync
        // is a no-op unless the leg is still Offering, so a terminal event that already claimed the leg for teardown wins.
        await PromoteOfferingAfterCommitAsync(voiceEvent.ChannelId);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Asterisk inbound real-time event {EventType} for provider {ProviderName} call {CallId} was offered to Contact Center.",
                voiceEvent.EventType.SanitizeLogValue(),
                voiceEvent.ProviderName,
                voiceEvent.CallId.SanitizeLogValue());
        }

        return true;
    }

    private async Task PromoteOfferingAfterCommitAsync(string channelId)
    {
        // Order the Offering->Connected promotion after the ambient scope's interaction commit. When there is no
        // ambient scope (a direct in-process invocation), the interaction is already durable, so promote inline.
        if (ShellScope.Current is null)
        {
            await _bindingStore.TryPromoteOfferingAsync(channelId);

            return;
        }

        ShellScope.AddDeferredTask(_ => _bindingStore.TryPromoteOfferingAsync(channelId));
    }

    private async Task TerminateOfferAsync(
        AsteriskRealtimeVoiceEvent voiceEvent,
        string holdingBridgeId,
        bool answerAttempted,
        bool bridgeCreateAttempted,
        bool provisioningOutcomeAmbiguous)
    {
        // Track whether every best-effort ARI cleanup genuinely succeeded. The durable Offering binding is removed
        // only when it did: a transient failure (or a lost answer/create ack that left the caller or holding bridge
        // live server-side) leaves the record in place so the reconciler resolves the aged, never-routed offer
        // rather than deleting the only record that tracks a resource still live on Asterisk.
        var cleaned = true;

        if (answerAttempted &&
            !await TryCompensateAsync(
                () => _ariClient.HangupAsync(voiceEvent.ChannelId, CancellationToken.None),
                "hang up inbound caller",
                voiceEvent.CallId))
        {
            cleaned = false;
        }

        if (bridgeCreateAttempted &&
            !await TryCompensateAsync(
                () => _ariClient.DestroyBridgeAsync(holdingBridgeId, CancellationToken.None),
                "destroy inbound holding bridge",
                voiceEvent.CallId))
        {
            cleaned = false;
        }

        // Retain the durable Offering binding when the provisioning outcome was transport-ambiguous: the ARI client
        // treats an already-gone resource as a successful compensation, but an ambiguous provisioning call (a client
        // timeout, or a transport error that returned no server response) may still commit on Asterisk AFTER this
        // sweep, so a "successful" hang-up or bridge destroy here does not prove the caller or holding bridge is
        // absent. Keeping the record lets the age-gated reconciler re-probe live ARI state and remove a real orphan.
        // Otherwise, remove the record only when every best-effort cleanup genuinely succeeded.
        if (cleaned && !provisioningOutcomeAmbiguous)
        {
            await TryCompensateAsync(
                () => _bindingStore.RemoveByChannelIdAsync(voiceEvent.ChannelId),
                "remove inbound channel binding",
                voiceEvent.CallId);
        }
    }

    private async Task<bool> TryCompensateAsync(
        Func<Task> action,
        string operation,
        string callId)
    {
        try
        {
            await action();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Asterisk failed to {Operation} while compensating inbound call {CallId}.",
                operation.SanitizeLogValue(),
                callId.SanitizeLogValue());

            return false;
        }
    }

    private async Task<bool> TryTerminateStrandedCallerAsync(AsteriskRealtimeVoiceEvent voiceEvent)
    {
        // Claim the channel for termination first. The claim is planted atomically with respect to a concurrent
        // create — under the store's per-channel lock, but only across a fast in-memory decision, never across the
        // remote hang up — so it can never starve an unrelated channel that hashes to the same stripe. Once claimed,
        // CreateAsync refuses to create a binding for the channel, so no delivery can route the caller into a live
        // call that this hang up would then tear down: the recover-versus-terminate decision is made once, here.
        bool claimed;

        try
        {
            claimed = await _bindingStore.TryClaimChannelForTerminationAsync(voiceEvent.ChannelId, CancellationToken.None);
        }
        catch (AsteriskChannelBindingCreateTimeoutException)
        {
            // The per-channel lock is still wedged by another create for this channel: do not spin inline. Enqueue so
            // the reconciliation sweep re-attempts the claim-and-hang-up over time once the lock frees.
            return false;
        }

        // A binding already exists: a different delivery legitimately recovered this caller into a live, owned call.
        // It must not be hung up. Drop it from the pending set so the reconciler does not later re-affirm a claim for
        // a channel that has been recovered.
        if (!claimed)
        {
            _pendingCallerTerminationRegistry.Resolve(voiceEvent.ChannelId);

            return true;
        }

        // The channel is claimed, so creates are now refused and the caller cannot be recovered concurrently: the hang
        // up runs OUTSIDE any lock. The ARI client maps an already-gone channel to a successful hang up, so a returned
        // failure is a genuine transient transport error; retry a bounded number of times before deferring to the
        // reconciliation sweep. The claim is released, and the channel resolved out of the pending set, only once the
        // caller is confirmed gone.
        for (var attempt = 1; attempt <= StrandedCallerHangupAttempts; attempt++)
        {
            if (await TryCompensateAsync(
                () => _ariClient.HangupAsync(voiceEvent.ChannelId, CancellationToken.None),
                "hang up inbound caller after create-lock timeout",
                voiceEvent.CallId))
            {
                _bindingStore.ReleaseTerminationClaim(voiceEvent.ChannelId);
                _pendingCallerTerminationRegistry.Resolve(voiceEvent.ChannelId);

                return true;
            }
        }

        // Every inline attempt failed. Keep the claim (so no delivery routes the caller while it is still live) and
        // leave the caller queued for the reconciliation sweep, which re-affirms the claim, retries the hang up, and
        // releases the claim.
        return false;
    }
}
