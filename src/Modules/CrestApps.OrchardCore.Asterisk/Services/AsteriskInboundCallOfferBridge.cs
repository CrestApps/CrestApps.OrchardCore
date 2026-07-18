using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Bridges first-seen inbound Asterisk caller channels into the Contact Center inbound voice pipeline.
/// </summary>
internal sealed class AsteriskInboundCallOfferBridge : IAsteriskRealtimeVoiceEventBridge
{
    private readonly IAsteriskChannelTenantBindingStore _bindingStore;
    private readonly IAsteriskAriClient _ariClient;
    private readonly IInboundVoiceEventSink _inboundVoiceEventSink;
    private readonly IClock _clock;
    private readonly ILogger<AsteriskInboundCallOfferBridge> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsteriskInboundCallOfferBridge"/> class.
    /// </summary>
    /// <param name="bindingStore">The tenant-scoped channel binding store.</param>
    /// <param name="ariClient">The tenant-scoped Asterisk ARI client.</param>
    /// <param name="inboundVoiceEventSink">The Contact Center inbound voice event sink.</param>
    /// <param name="clock">The clock used to stamp tenant-owned state.</param>
    /// <param name="logger">The logger instance.</param>
    public AsteriskInboundCallOfferBridge(
        IAsteriskChannelTenantBindingStore bindingStore,
        IAsteriskAriClient ariClient,
        IInboundVoiceEventSink inboundVoiceEventSink,
        IClock clock,
        ILogger<AsteriskInboundCallOfferBridge> logger)
    {
        _bindingStore = bindingStore;
        _ariClient = ariClient;
        _inboundVoiceEventSink = inboundVoiceEventSink;
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

        var existing = await _bindingStore.FindByChannelIdAsync(voiceEvent.ChannelId);

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
        var created = await _bindingStore.CreateAsync(new AsteriskChannelTenantBinding
        {
            ChannelId = voiceEvent.ChannelId,
            ProviderName = voiceEvent.ProviderName,
            InteractionId = voiceEvent.InteractionCorrelationId,
            ProviderCallId = voiceEvent.CallId,
            State = AsteriskChannelBindingState.Offering,
            CreatedUtc = _clock.UtcNow,
        });

        if (!created)
        {
            // Another delivery of this StasisStart already claimed the channel and owns its offer flow. The racy
            // fast-path check above can let two overlapping same-tenant listener generations both reach here, so the
            // atomic create is the authoritative single-winner claim: the caller that loses it must not answer, create
            // the holding bridge, park, or route the caller a second time.
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
                OperationalLogRedactor.RedactException(ex),
                "Asterisk failed to offer inbound call {CallId}; terminating the caller.",
                OperationalLogRedactor.Pseudonymize(voiceEvent.CallId, OperationalLogIdentifierCategory.Call));

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
                OperationalLogRedactor.RedactException(ex),
                "Asterisk inbound routing failed after call {CallId} was parked; retaining the offer for reconciliation.",
                OperationalLogRedactor.Pseudonymize(voiceEvent.CallId, OperationalLogIdentifierCategory.Call));

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
                OperationalLogRedactor.Redact(voiceEvent.EventType, OperationalLogFieldKind.FreeText),
                voiceEvent.ProviderName,
                OperationalLogRedactor.Pseudonymize(voiceEvent.CallId, OperationalLogIdentifierCategory.Call));
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
                OperationalLogRedactor.RedactException(ex),
                "Asterisk failed to {Operation} while compensating inbound call {CallId}.",
                OperationalLogRedactor.Redact(operation, OperationalLogFieldKind.FreeText),
                OperationalLogRedactor.Pseudonymize(callId, OperationalLogIdentifierCategory.Call));

            return false;
        }
    }
}
