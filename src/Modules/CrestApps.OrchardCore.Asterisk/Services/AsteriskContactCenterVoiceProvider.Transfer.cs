using System.Globalization;
using System.Text;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Asterisk.Services;

internal sealed partial class AsteriskContactCenterVoiceProvider
{
    /// <inheritdoc/>
    public async Task<ContactCenterVoiceProviderResult> TransferAsync(
        ContactCenterVoiceTransferRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var workLease = _workManager.TryEnter(AsteriskConstants.Feature.ContactCenterVoice);

        if (workLease is null)
        {
            return Failure("feature_quiescing", "The Asterisk Contact Center voice provider is temporarily unavailable.");
        }

        if (string.IsNullOrWhiteSpace(request.ProviderCallId))
        {
            return Failure("caller_channel_missing", "An Asterisk caller channel id is required to transfer the call.");
        }

        if (string.IsNullOrWhiteSpace(request.InteractionId))
        {
            return Failure("interaction_missing", "An interaction id is required to transfer the call.");
        }

        // Only a blind transfer to an agent is executable on Asterisk today. Consultative (warm) transfer needs a
        // two-phase consult/complete contract the single-call provider boundary does not express, and external,
        // queue, and entry-point destinations need trunk origination or a re-route media path that is not yet built.
        // Reject those as CONFIRMED failures (never OutcomeUnknown) so the transfer service surfaces a clear,
        // non-ambiguous result instead of leaving the call in an unknown state.
        if (request.TransferType != InteractionTransferType.Blind)
        {
            return Failure("transfer_type_unsupported", "The Asterisk provider only supports blind transfers.");
        }

        if (request.TargetType != InteractionTransferTargetType.Agent)
        {
            return Failure("transfer_target_unsupported", "The Asterisk provider only supports transferring a call to an agent.");
        }

        if (request.Metadata is null ||
            !request.Metadata.TryGetValue(ContactCenterConstants.TransferMetadata.AgentUserId, out var targetUserId) ||
            string.IsNullOrWhiteSpace(targetUserId))
        {
            return Failure("transfer_target_missing", "A destination agent is required to transfer the call.");
        }

        var callerChannelId = request.ProviderCallId.Trim();

        // Resolve the canonical conversation bridge and the current agent leg from a binding owned by THIS tenant's
        // store BEFORE resolving anything about the destination. Failing closed here first enforces CC-1 — an agent
        // can never transfer a call this tenant does not own — and guarantees an unowned call always fails with the
        // single unambiguous ownership reason rather than leaking the destination's registration state.
        var currentLeg = await ResolveOwnedAgentLegAsync(callerChannelId);

        if (currentLeg is null)
        {
            return Failure("transfer_call_not_owned", "No owned Asterisk conversation bridge was found for the requested transfer.");
        }

        var bridgeId = currentLeg.BridgeId;
        var currentAgentChannelId = currentLeg.ChannelId;
        var newAgentChannelId = CreateDeterministicAriId(
            AsteriskAriConstants.TransferAgentChannelPrefix,
            string.Concat(request.InteractionId, "-", targetUserId));

        // A retried transfer to the same destination must be idempotent: if the current agent leg IS the leg this
        // transfer would create (its deterministic id), the transfer already completed, so re-originating would ring
        // the destination a second time. Confirm the completed transfer instead.
        if (string.Equals(newAgentChannelId, currentAgentChannelId, StringComparison.Ordinal))
        {
            return TransferSuccess(newAgentChannelId, bridgeId);
        }

        var destinationEndpoint = await ResolveLiveSoftphoneEndpointAsync(targetUserId, cancellationToken);

        if (string.IsNullOrWhiteSpace(destinationEndpoint))
        {
            return Failure("transfer_target_offline", "The destination agent has no live Asterisk softphone registration to transfer the call to.");
        }

        // Persist the transfer's exactly-once ownership claim on the deterministic destination channel id BEFORE
        // originating. The durable Joining binding is BOTH the per-conversation transfer claim — a concurrent
        // duplicate transfer to the same destination loses this serialized create and must not re-ring the
        // destination — AND the guarantee that ANY death of the destination leg (before, during, or after bridging
        // but before the handoff commits) is a teardown no-op that leaves the caller with the current agent, because
        // a Joining leg never owns the shared canonical bridge. The BridgeId is recorded so recording/monitoring and
        // the atomic swap can resolve the conversation, but it is not an ownership claim while the state is Joining.
        var claimedNewLeg = await _channelTenantBindingStore.CreateAsync(new AsteriskChannelTenantBinding
        {
            ChannelId = newAgentChannelId,
            ProviderName = TechnicalName,
            InteractionId = request.InteractionId,
            ProviderCallId = callerChannelId,
            BridgeId = bridgeId,
            PeerChannelId = callerChannelId,
            State = AsteriskChannelBindingState.Joining,
            CreatedUtc = _clock.UtcNow,
        });

        if (!claimedNewLeg)
        {
            // The serialized durable create lost. Distinguish an ACTIVE concurrent duplicate transfer to the same
            // destination (an in-flight Joining claim — the handoff outcome is identical, so confirm success without
            // re-ringing the destination or disturbing the concurrent transfer's leg) from a STALE claim: a prior
            // attempt's destination leg whose terminal event left a Terminating recovery record for the reconciler to
            // reclaim. Reporting success for a stale claim would falsely confirm a transfer that has no live
            // destination leg while the previous agent still owns the call, so fail closed (confirmed, retryable)
            // instead and let the caller retry once the stale claim is retired.
            var existingClaim = await _channelTenantBindingStore.FindByChannelIdAsync(newAgentChannelId);

            if (existingClaim is not null && existingClaim.State == AsteriskChannelBindingState.Joining)
            {
                return TransferSuccess(newAgentChannelId, bridgeId);
            }

            return Failure("transfer_failed", "A previous transfer to this destination is still being reclaimed; retry the transfer shortly.");
        }

        var originateAttempted = false;
        var handoffCommitted = false;

        try
        {
            // Register readiness before originating so the destination leg's StasisStart can never be missed between
            // the originate call returning and the wait beginning. The originate uses our deterministic channel id,
            // so the readiness key matches the id the StasisStart will carry.
            using var readyRegistration = _agentChannelReadySignal.Register(newAgentChannelId);

            var originateRequest = new AsteriskAriOriginateRequest
            {
                Endpoint = destinationEndpoint,
                CallerId = callerChannelId,
                ChannelId = newAgentChannelId,
                AppArgs = [AsteriskConstants.OriginationMarkerVariableName, request.InteractionId, "agent"],
                Variables = new Dictionary<string, string>
                {
                    [AsteriskConstants.OriginationMarkerVariableName] = AsteriskAriConstants.OriginationMarkerValue,
                    [AsteriskConstants.InteractionChannelVariableName] = request.InteractionId,
                },
            };

            // Mark the originate as attempted BEFORE issuing it: an ambiguous transport failure (no ARI response) may
            // still have created the destination channel under our deterministic id, so the compensation path must be
            // able to best-effort release it even though the originate never returned success.
            originateAttempted = true;

            await _ariClient.OriginateAsync(originateRequest, cancellationToken);

            // The destination can only be bridged once it has entered Stasis (that is, once the destination agent
            // answers), so wait for its owned-origination StasisStart bounded by the answer timeout. The customer
            // keeps talking to the current agent on the canonical bridge until the destination answers, so a blind
            // transfer to an agent who never answers leaves the ORIGINAL call fully intact.
            var destinationReady = await readyRegistration.WaitAsync(
                TimeSpan.FromSeconds(AsteriskAriConstants.AgentAnswerTimeoutSeconds),
                cancellationToken);

            if (destinationReady != AsteriskAgentChannelReadyOutcome.Ready)
            {
                // A cancelled wait surfaces as a not-ready result, so distinguish genuine no-answer from cancellation
                // and let cancellation propagate to the unknown-outcome path instead of reporting a confirmed timeout.
                cancellationToken.ThrowIfCancellationRequested();

                // The destination leg was originated and is ringing (it simply never answered), so its channel is
                // live. Best-effort hang it up; if that hangup is not confirmed, keep a durable recovery record so
                // the reconciler reclaims the dangling channel instead of leaking it.
                var noAnswerHungUp = await TryHangupAsync(newAgentChannelId, CancellationToken.None);
                await RetireProvisionalTransferClaimAsync(newAgentChannelId, noAnswerHungUp);

                return Failure("transfer_no_answer", "The destination agent did not answer before the transfer timed out.");
            }

            // Commit point: add the answered destination leg to the SAME canonical conversation bridge the customer is
            // already on, so recording stays on one continuous bridge and the customer never leaves the recorded
            // conversation. The destination binding is still Joining (non-owning), so a destination death anywhere in
            // this window is a teardown no-op that leaves the customer with the current agent. The handoff becomes
            // durable only when FinalizeTransferHandoffAsync atomically promotes it and retires the previous leg.
            await _ariClient.AddChannelToBridgeAsync(bridgeId, newAgentChannelId, cancellationToken);
            handoffCommitted = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Asterisk failed to transfer interaction {InteractionId}; compensating the new destination leg.",
                request.InteractionId.SanitizeLogValue());

            // Pre-commit compensation. Because the serialized durable Joining create above gave THIS transfer
            // exclusive ownership of the deterministic destination id (a concurrent duplicate lost the create and
            // never originated), releasing our own destination leg can never touch another transfer's live call — no
            // existence probe or winner check is needed. A Joining leg never owns the shared bridge, so compensating
            // in any order can never drop the customer, who stays with the current agent on the canonical bridge.
            if (!handoffCommitted)
            {
                // The destination channel may be live whenever the originate was attempted and Asterisk did not
                // DEFINITELY reject it: a received 4xx/5xx response (non-null status) proves no channel was created
                // under our id, but a transport-ambiguous or cancelled originate may still have created it. Hang up
                // the leg only when it may exist, and keep a durable recovery record whenever its disposition is
                // unconfirmed so the reconciler reclaims a leaked channel instead of hard-deleting the only record
                // that could find it.
                var channelDefinitelyRejected = ex is AsteriskAriException rejected && rejected.StatusCode is not null;
                var channelMayExist = originateAttempted && !channelDefinitelyRejected;
                var channelConfirmedGone = true;

                if (channelMayExist)
                {
                    channelConfirmedGone = await TryHangupAsync(newAgentChannelId, CancellationToken.None);
                }

                await RetireProvisionalTransferClaimAsync(newAgentChannelId, channelConfirmedGone);
            }

            if (ex is OperationCanceledException)
            {
                throw;
            }

            var outcomeUnknown = ex is AsteriskAriException ariException && IsAmbiguousAriOutcome(ariException);

            return new ContactCenterVoiceProviderResult
            {
                Succeeded = false,
                OutcomeUnknown = outcomeUnknown,
                ProviderName = TechnicalName,
                ProviderCallId = callerChannelId,
                ErrorCode = outcomeUnknown ? "transfer_outcome_unknown" : "transfer_failed",
                ErrorMessage = "The Asterisk call transfer could not be completed.",
            };
        }

        // Post-commit finalization: the destination leg is bridged with the customer, so the transfer has physically
        // succeeded and is reported as success. Ownership is transferred atomically and the previous leg retired on a
        // best-effort basis — never rolling back the new leg — leaving any residue to the terminal-event/reconciler
        // path, exactly as the connect flow tolerates post-commit ARI hiccups.
        await FinalizeTransferHandoffAsync(
            currentAgentChannelId,
            newAgentChannelId,
            request.InteractionId);

        return TransferSuccess(newAgentChannelId, bridgeId);
    }

    private async Task FinalizeTransferHandoffAsync(
        string currentAgentChannelId,
        string newAgentChannelId,
        string interactionId)
    {
        try
        {
            // Atomically promote the destination leg from Joining to Connected AND retire the previous agent leg in
            // ONE transaction, so ownership of the canonical conversation bridge transfers from the previous agent to
            // the destination agent with no instant of two Connected owners (a double-teardown drop) or zero owners
            // (an unowned live bridge). Until this commits the destination leg is Joining and non-owning, so the
            // customer is always owned by exactly one Connected binding — the previous agent before the swap, the
            // destination agent after it.
            var swapped = await _channelTenantBindingStore.SwapConnectedOwnerAsync(newAgentChannelId, currentAgentChannelId);

            if (!swapped)
            {
                // The destination leg was no longer Joining — a terminal event claimed it for teardown between the
                // bridge commit and this swap. The previous agent leg is deliberately left as the Connected owner, so
                // the customer safely keeps the previous agent and the dangling destination residue is reclaimed by
                // the reconciler. Never a drop, never a double owner.
                _logger.LogWarning(
                    "Asterisk transfer of interaction {InteractionId} could not finalize the handoff because the destination leg was already claimed for teardown; leaving the previous agent as the owner.",
                    interactionId.SanitizeLogValue());

                return;
            }

            // The destination leg now solely owns the canonical bridge, and the previous leg's binding was atomically
            // retired to a NON-OWNING Terminating (Joining-disposition) recovery record, so hanging up the previous
            // leg cannot tear down the conversation — its terminal event finds no owning binding and only releases its
            // own channel. Best-effort hang up the previous channel; when that hangup is confirmed remove its now
            // redundant recovery record, and when it is unconfirmed leave the durable record so the reconciler
            // reclaims the possibly-live previous channel instead of leaking it. This is the same post-commit,
            // update-then-call-ARI residual the connect flow tolerates.
            var previousHungUp = await TryHangupAsync(currentAgentChannelId, CancellationToken.None);
            await RetireProvisionalTransferClaimAsync(currentAgentChannelId, previousHungUp);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Asterisk transfer of interaction {InteractionId} bridged the destination agent but did not finalize the previous leg cleanly; leaving residue for the reconciler.",
                interactionId.SanitizeLogValue());
        }
    }

    private static ContactCenterVoiceProviderResult TransferSuccess(string newAgentChannelId, string bridgeId)
    {
        return new ContactCenterVoiceProviderResult
        {
            Succeeded = true,
            ProviderName = AsteriskConstants.ProviderTechnicalName,
            ProviderCallId = newAgentChannelId,
            ProviderLegId = newAgentChannelId,
            Metadata = new Dictionary<string, string>
            {
                [AsteriskVoiceResultMetadata.TransferNewChannelId] = newAgentChannelId,
                [AsteriskVoiceResultMetadata.TransferBridgeId] = bridgeId,
            },
        };
    }
}
