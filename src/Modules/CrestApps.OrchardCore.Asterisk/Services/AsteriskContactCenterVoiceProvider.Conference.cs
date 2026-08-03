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
    public async Task<ContactCenterVoiceProviderResult> ConferenceAsync(
        ContactCenterVoiceConferenceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var workLease = _workManager.TryEnter(AsteriskConstants.Feature.ContactCenterVoice);

        if (workLease is null)
        {
            return Failure("feature_quiescing", "The Asterisk Contact Center voice provider is temporarily unavailable.");
        }

        if (string.IsNullOrWhiteSpace(request.InteractionId))
        {
            return Failure("interaction_missing", "An interaction id is required to add a conference participant.");
        }

        // The caller channel identifies the live conversation the participant joins. Only the caller leg is needed to
        // resolve the canonical bridge and its owner; any additional provider call ids in the request are ignored.
        var callerChannelId = request.ProviderCallIds?
            .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id))?
            .Trim();

        if (string.IsNullOrWhiteSpace(callerChannelId))
        {
            return Failure("caller_channel_missing", "An Asterisk caller channel id is required to add a conference participant.");
        }

        if (request.Metadata is null ||
            !request.Metadata.TryGetValue(ContactCenterConstants.ConferenceMetadata.AgentUserId, out var targetUserId) ||
            string.IsNullOrWhiteSpace(targetUserId))
        {
            return Failure("conference_target_missing", "A participant agent is required to add to the conference.");
        }

        // Resolve the canonical conversation bridge and its current owner from a binding owned by THIS tenant's store
        // BEFORE resolving anything about the destination. Failing closed here first enforces CC-1 — an agent can
        // never add a participant to a call this tenant does not own — and guarantees an unowned call always fails
        // with the single unambiguous ownership reason rather than leaking the destination's registration state.
        var currentLeg = await ResolveOwnedAgentLegAsync(callerChannelId);

        if (currentLeg is null)
        {
            return Failure("conference_call_not_owned", "No owned Asterisk conversation bridge was found for the requested conference.");
        }

        var bridgeId = currentLeg.BridgeId;
        var participantChannelId = CreateDeterministicAriId(
            AsteriskAriConstants.ConferenceParticipantChannelPrefix,
            string.Concat(request.InteractionId, "-", targetUserId));

        return await AddAgentToConversationAsync(
            request.InteractionId,
            callerChannelId,
            bridgeId,
            participantChannelId,
            targetUserId,
            "conference",
            ConferenceSuccess,
            cancellationToken);
    }

    /// <summary>
    /// Rings a resolved agent into a live conversation as a non-owning leg on the canonical bridge, shared by the
    /// conference-add and attended-transfer consult flows. The <paramref name="operation"/> word forms the returned
    /// error codes (<c>{operation}_target_offline</c>, <c>{operation}_no_answer</c>, <c>{operation}_failed</c>,
    /// <c>{operation}_outcome_unknown</c>) so each caller keeps its own stable code surface, while the ownership,
    /// idempotency, and compensation invariants are identical because both flows add a Joining leg that never owns
    /// the shared bridge.
    /// </summary>
    /// <param name="interactionId">The interaction whose live conversation the agent joins.</param>
    /// <param name="callerChannelId">The caller channel that anchors the canonical conversation bridge.</param>
    /// <param name="bridgeId">The canonical conversation bridge identifier.</param>
    /// <param name="participantChannelId">The deterministic channel id to originate the agent leg onto.</param>
    /// <param name="targetUserId">The Orchard user id of the agent to add.</param>
    /// <param name="operation">The operation word used to form stable error codes.</param>
    /// <param name="successFactory">Builds the caller-specific success result from the participant channel and bridge.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The provider operation result.</returns>
    private async Task<ContactCenterVoiceProviderResult> AddAgentToConversationAsync(
        string interactionId,
        string callerChannelId,
        string bridgeId,
        string participantChannelId,
        string targetUserId,
        string operation,
        Func<string, string, ContactCenterVoiceProviderResult> successFactory,
        CancellationToken cancellationToken)
    {
        // A retried add for the same participant must be idempotent: if the deterministic participant leg already
        // exists as a live member (a stabilized Participating leg, or the Connected owner when the target agent is
        // already on the call), the add already completed, so re-originating would ring the agent a second time.
        // Confirm the completed add instead.
        var existingMember = await _channelTenantBindingStore.FindByChannelIdAsync(participantChannelId);

        if (existingMember is not null &&
            (existingMember.State == AsteriskChannelBindingState.Participating ||
                existingMember.State == AsteriskChannelBindingState.Connected))
        {
            return successFactory(participantChannelId, bridgeId);
        }

        var destinationEndpoint = await ResolveLiveSoftphoneEndpointAsync(targetUserId, cancellationToken);

        if (string.IsNullOrWhiteSpace(destinationEndpoint))
        {
            return Failure($"{operation}_target_offline", "The target agent has no live Asterisk softphone registration to add to the conversation.");
        }

        // Persist the exactly-once ownership claim on the deterministic participant channel id BEFORE originating, as
        // a NON-OWNING Joining leg. The durable Joining binding is BOTH the per-agent add claim — a concurrent
        // duplicate add for the same agent loses this serialized create and must not re-ring — AND the guarantee that
        // ANY death of the participant leg (before, during, or after bridging but before it is stabilized) is a
        // teardown no-op that leaves the live conversation intact, because a Joining leg never owns the shared
        // canonical bridge. The BridgeId is recorded so the leg can be added to the conversation and later promoted,
        // but it is not an ownership claim while the state is Joining.
        var claimed = await _channelTenantBindingStore.CreateAsync(new AsteriskChannelTenantBinding
        {
            ChannelId = participantChannelId,
            ProviderName = TechnicalName,
            InteractionId = interactionId,
            ProviderCallId = callerChannelId,
            BridgeId = bridgeId,
            PeerChannelId = callerChannelId,
            State = AsteriskChannelBindingState.Joining,
            CreatedUtc = _clock.UtcNow,
        });

        if (!claimed)
        {
            // The serialized durable create lost. Distinguish an ACTIVE concurrent duplicate add for the same agent
            // (an in-flight Joining claim or an already-stabilized live member — the outcome is identical, so confirm
            // success without re-ringing) from a STALE claim: a prior attempt's participant leg whose terminal event
            // left a Terminating recovery record for the reconciler to reclaim. Reporting success for a stale claim
            // would falsely confirm a participant that has no live leg, so fail closed (confirmed, retryable) instead
            // and let the caller retry once the stale claim is retired.
            var existingClaim = await _channelTenantBindingStore.FindByChannelIdAsync(participantChannelId);

            if (existingClaim is not null &&
                (existingClaim.State == AsteriskChannelBindingState.Joining ||
                    existingClaim.State == AsteriskChannelBindingState.Participating ||
                    existingClaim.State == AsteriskChannelBindingState.Connected))
            {
                return successFactory(participantChannelId, bridgeId);
            }

            return Failure($"{operation}_failed", "A previous add for this agent is still being reclaimed; retry shortly.");
        }

        var originateAttempted = false;
        var participantBridged = false;

        try
        {
            // Register readiness before originating so the participant leg's StasisStart can never be missed between
            // the originate call returning and the wait beginning. The originate uses our deterministic channel id,
            // so the readiness key matches the id the StasisStart will carry.
            using var readyRegistration = _agentChannelReadySignal.Register(participantChannelId);

            var originateRequest = new AsteriskAriOriginateRequest
            {
                Endpoint = destinationEndpoint,
                CallerId = callerChannelId,
                ChannelId = participantChannelId,
                AppArgs = [AsteriskConstants.OriginationMarkerVariableName, interactionId, "agent"],
                Variables = new Dictionary<string, string>
                {
                    [AsteriskConstants.OriginationMarkerVariableName] = AsteriskAriConstants.OriginationMarkerValue,
                    [AsteriskConstants.InteractionChannelVariableName] = interactionId,
                },
            };

            // Mark the originate as attempted BEFORE issuing it: an ambiguous transport failure (no ARI response) may
            // still have created the participant channel under our deterministic id, so the compensation path must be
            // able to best-effort release it even though the originate never returned success.
            originateAttempted = true;

            await _ariClient.OriginateAsync(originateRequest, cancellationToken);

            // The participant can only be bridged once it has entered Stasis (that is, once the participant agent
            // answers), so wait for its owned-origination StasisStart bounded by the answer timeout. The existing
            // parties keep talking on the canonical bridge until the participant answers, so an add to an agent who
            // never answers leaves the ORIGINAL conversation fully intact.
            var participantReady = await readyRegistration.WaitAsync(
                TimeSpan.FromSeconds(AsteriskAriConstants.AgentAnswerTimeoutSeconds),
                cancellationToken);

            if (participantReady != AsteriskAgentChannelReadyOutcome.Ready)
            {
                // A cancelled wait surfaces as a not-ready result, so distinguish genuine no-answer from cancellation
                // and let cancellation propagate to the unknown-outcome path instead of reporting a confirmed timeout.
                cancellationToken.ThrowIfCancellationRequested();

                // The participant leg was originated and is ringing (it simply never answered), so its channel is
                // live. Best-effort hang it up; if that hangup is not confirmed, keep a durable recovery record so
                // the reconciler reclaims the dangling channel instead of leaking it.
                var noAnswerHungUp = await TryHangupAsync(participantChannelId, CancellationToken.None);
                await RetireProvisionalTransferClaimAsync(participantChannelId, noAnswerHungUp);

                return Failure($"{operation}_no_answer", "The target agent did not answer before the add timed out.");
            }

            // Commit point: add the answered participant leg to the SAME canonical conversation bridge the existing
            // parties are already on, so recording stays on one continuous bridge and every party hears the new
            // participant. The participant binding is still Joining (non-owning), so a participant death anywhere in
            // this window is a teardown no-op that leaves the existing conversation intact. The add becomes a durable
            // member only when FinalizeConferenceParticipantAsync promotes it to the stable Participating phase.
            await _ariClient.AddChannelToBridgeAsync(bridgeId, participantChannelId, cancellationToken);
            participantBridged = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Asterisk failed to add an agent to interaction {InteractionId}; compensating the participant leg.",
                interactionId.SanitizeLogValue());

            // Pre-commit compensation. Because the serialized durable Joining create above gave THIS add exclusive
            // ownership of the deterministic participant id (a concurrent duplicate lost the create and never
            // originated), releasing our own participant leg can never touch another add's live call — no existence
            // probe or winner check is needed. A Joining leg never owns the shared bridge, so compensating in any
            // order can never drop the existing conversation.
            if (!participantBridged)
            {
                var channelDefinitelyRejected = ex is AsteriskAriException rejected && rejected.StatusCode is not null;
                var channelMayExist = originateAttempted && !channelDefinitelyRejected;
                var channelConfirmedGone = true;

                if (channelMayExist)
                {
                    channelConfirmedGone = await TryHangupAsync(participantChannelId, CancellationToken.None);
                }

                await RetireProvisionalTransferClaimAsync(participantChannelId, channelConfirmedGone);
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
                ErrorCode = outcomeUnknown ? $"{operation}_outcome_unknown" : $"{operation}_failed",
                ErrorMessage = "The Asterisk add of an agent to the conversation could not be completed.",
            };
        }

        // Post-commit stabilization: the participant leg is bridged with the existing parties, so the add has
        // physically succeeded and is reported as success. Promote it from the provisioning Joining phase to the
        // stable NON-OWNING Participating phase so the reconciler treats the live member as healthy rather than an
        // aged, never-committed join to reclaim — never rolling back the bridged leg — leaving any residue to the
        // terminal-event/reconciler path, exactly as the transfer flow tolerates post-commit ARI hiccups.
        await FinalizeConferenceParticipantAsync(participantChannelId, interactionId);

        return successFactory(participantChannelId, bridgeId);
    }

    private async Task FinalizeConferenceParticipantAsync(string participantChannelId, string interactionId)
    {
        try
        {
            // Promote the bridged participant leg from Joining to the stable non-owning Participating phase. Until
            // this commits the leg is Joining and the reconciler would age-reclaim a still-alive participant as a
            // never-committed join; promoting it makes the reconciler treat the live member as healthy.
            var promoted = await _channelTenantBindingStore.TryPromoteJoiningToParticipatingAsync(participantChannelId);

            if (!promoted)
            {
                // A terminal event claimed the participant leg for teardown between the bridge commit and this
                // promotion. The dangling participant residue is reclaimed by the reconciler; the existing parties
                // are unaffected because a Joining/Participating leg never owns the shared bridge, so the physically
                // committed add is still a success.
                _logger.LogWarning(
                    "Asterisk conference add of interaction {InteractionId} bridged the participant but could not stabilize it because its leg was already claimed for teardown; leaving the residue for the reconciler.",
                    interactionId.SanitizeLogValue());
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Asterisk conference add of interaction {InteractionId} bridged the participant but did not stabilize its leg cleanly; leaving residue for the reconciler.",
                interactionId.SanitizeLogValue());
        }
    }

    private static ContactCenterVoiceProviderResult ConferenceSuccess(string participantChannelId, string bridgeId)
    {
        return new ContactCenterVoiceProviderResult
        {
            Succeeded = true,
            ProviderName = AsteriskConstants.ProviderTechnicalName,
            Metadata = new Dictionary<string, string>
            {
                [AsteriskVoiceResultMetadata.ConferenceParticipantChannelId] = participantChannelId,
                [AsteriskVoiceResultMetadata.ConferenceBridgeId] = bridgeId,
            },
        };
    }
}
