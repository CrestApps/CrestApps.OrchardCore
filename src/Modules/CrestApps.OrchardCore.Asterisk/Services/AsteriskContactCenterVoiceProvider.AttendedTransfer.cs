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
    public async Task<ContactCenterVoiceProviderResult> BeginConsultAsync(
        ContactCenterVoiceAttendedTransferRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var workLease = _workManager.TryEnter(AsteriskConstants.Feature.ContactCenterVoice);

        if (workLease is null)
        {
            return Failure("feature_quiescing", "The Asterisk Contact Center voice provider is temporarily unavailable.");
        }

        var (failure, context) = await ResolveConsultContextAsync(request, "consult");

        if (failure is not null)
        {
            return failure;
        }

        // Hold the customer BEFORE ringing the destination agent so the private consult can never be heard by the
        // customer. Holding fails closed: if the hold does not commit, the destination is never rung, leaving the
        // original conversation fully intact rather than opening a three-way call the customer can overhear.
        var held = await TryHoldAsync(context.CallerChannelId, cancellationToken);

        if (!held)
        {
            return Failure("consult_hold_failed", "The customer could not be placed on hold to begin the private consult.");
        }

        ContactCenterVoiceProviderResult addResult;

        try
        {
            // Ring the destination agent into the SAME canonical bridge as a non-owning participant, reusing the
            // conference-add core. The customer is held, so the destination agent and the initiating agent hold a
            // private conversation; if the destination never answers the add leaves the original call intact.
            addResult = await AddAgentToConversationAsync(
                request.InteractionId,
                context.CallerChannelId,
                context.BridgeId,
                context.ConsultChannelId,
                context.TargetUserId,
                "consult",
                ConsultSuccess,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // The add compensated its own destination leg before rethrowing, but the customer is still held. Resume
            // the customer so a cancelled begin-consult never strands them on hold, then propagate the cancellation.
            await TryUnholdAsync(context.CallerChannelId, CancellationToken.None);

            throw;
        }

        if (!addResult.Succeeded)
        {
            // The destination agent did not join (offline, no answer, or a failed originate). The add already
            // compensated its own leg, so resume the customer with the initiating agent and surface the failure.
            await TryUnholdAsync(context.CallerChannelId, CancellationToken.None);

            return addResult;
        }

        return addResult;
    }

    /// <inheritdoc/>
    public async Task<ContactCenterVoiceProviderResult> CompleteConsultAsync(
        ContactCenterVoiceAttendedTransferRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var workLease = _workManager.TryEnter(AsteriskConstants.Feature.ContactCenterVoice);

        if (workLease is null)
        {
            return Failure("feature_quiescing", "The Asterisk Contact Center voice provider is temporarily unavailable.");
        }

        var (failure, context) = await ResolveConsultContextAsync(request, "consult_complete");

        if (failure is not null)
        {
            return failure;
        }

        // Resume the customer BEFORE the ownership swap: if the atomic hand-off cannot commit (the destination leg is
        // gone), the customer is already back in a live conversation with the initiating agent who still owns the
        // bridge, rather than being stranded on hold. Unhold is best-effort; a failed unhold is a recoverable degraded
        // state (the customer stays on hold with a live owner) that never drops the call.
        await TryUnholdAsync(context.CallerChannelId, cancellationToken);

        // Atomically promote the stabilized destination participant to the Connected owner AND retire the initiating
        // agent leg in one transaction, so the canonical bridge is owned by exactly one Connected binding at every
        // instant — the initiating agent before the swap, the destination agent after it.
        var swapped = await _channelTenantBindingStore.PromoteParticipantToConnectedOwnerAsync(
            context.ConsultChannelId,
            context.AgentChannelId);

        if (!swapped)
        {
            // The destination leg was no longer a live participant (the consult was never begun, or the destination
            // hung up), so ownership was not handed off. The customer safely keeps the initiating agent. This is a
            // CONFIRMED failure — the call is intact, just not transferred.
            return Failure("consult_complete_no_target", "No live consult leg was found to complete the attended transfer; the customer remains with the initiating agent.");
        }

        // The destination leg now solely owns the canonical bridge and the initiating leg was atomically retired to a
        // non-owning recovery record, so hanging up the initiating leg cannot tear down the conversation. Best-effort
        // hang it up and reconcile its record, exactly as the blind-transfer handoff does.
        var previousHungUp = await TryHangupAsync(context.AgentChannelId, CancellationToken.None);
        await RetireProvisionalTransferClaimAsync(context.AgentChannelId, previousHungUp);

        return ConsultSuccess(context.ConsultChannelId, context.BridgeId);
    }

    /// <inheritdoc/>
    public async Task<ContactCenterVoiceProviderResult> CancelConsultAsync(
        ContactCenterVoiceAttendedTransferRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var workLease = _workManager.TryEnter(AsteriskConstants.Feature.ContactCenterVoice);

        if (workLease is null)
        {
            return Failure("feature_quiescing", "The Asterisk Contact Center voice provider is temporarily unavailable.");
        }

        var (failure, context) = await ResolveConsultContextAsync(request, "consult_cancel");

        if (failure is not null)
        {
            return failure;
        }

        // Drop the destination consult leg first, but atomically and ONLY while it is a non-owning member leg. The
        // consult channel id is deterministic and is the SAME id CompleteConsultAsync promotes IN PLACE from
        // Participating to the sole Connected owner of the canonical bridge, so a bare read-then-hangup could hang up
        // that promoted owner and drop the live customer. TryClaimProvisionalLegForTeardownAsync is the linearization
        // point: it version-checks and transitions the leg to Terminating ONLY when it is still Joining/Participating,
        // so a completion that already (or concurrently) promoted it to Connected makes this claim fail and the cancel
        // never hangs up the leg. When the claim succeeds we own the teardown of this dangling channel.
        var claimedConsultLeg = await _channelTenantBindingStore.TryClaimProvisionalLegForTeardownAsync(context.ConsultChannelId);

        if (claimedConsultLeg)
        {
            var consultHungUp = await TryHangupAsync(context.ConsultChannelId, CancellationToken.None);

            if (consultHungUp)
            {
                // The channel is confirmed gone, so retire the durable teardown record. Otherwise leave the
                // Terminating record (with its non-owning pre-teardown disposition) for the reconciler to reclaim.
                await _channelTenantBindingStore.RemoveByChannelIdAsync(context.ConsultChannelId);
            }
        }

        // Resume the customer with the initiating agent, who remained the Connected owner throughout the consult, so
        // ownership is unchanged and the original conversation continues.
        await TryUnholdAsync(context.CallerChannelId, cancellationToken);

        return ConsultSuccess(context.ConsultChannelId, context.BridgeId);
    }

    private async Task<(ContactCenterVoiceProviderResult Failure, ResolvedConsultContext Context)> ResolveConsultContextAsync(
        ContactCenterVoiceAttendedTransferRequest request,
        string operation)
    {
        if (string.IsNullOrWhiteSpace(request.InteractionId))
        {
            return (Failure("interaction_missing", "An interaction id is required for the attended transfer."), null);
        }

        var callerChannelId = request.ProviderCallId?.Trim();

        if (string.IsNullOrWhiteSpace(callerChannelId))
        {
            return (Failure("caller_channel_missing", "An Asterisk caller channel id is required for the attended transfer."), null);
        }

        if (request.Metadata is null ||
            !request.Metadata.TryGetValue(ContactCenterConstants.AttendedTransferMetadata.AgentUserId, out var targetUserId) ||
            string.IsNullOrWhiteSpace(targetUserId))
        {
            return (Failure($"{operation}_target_missing", "A destination agent is required for the attended transfer."), null);
        }

        // Resolve the canonical conversation bridge and its current Connected owner from a binding owned by THIS
        // tenant's store. Failing closed here enforces CC-1 — an agent can never consult on a call this tenant does
        // not own — and guarantees an unowned call fails with the single unambiguous ownership reason.
        var currentLeg = await ResolveOwnedAgentLegAsync(callerChannelId);

        if (currentLeg is null)
        {
            return (Failure($"{operation}_call_not_owned", "No owned Asterisk conversation bridge was found for the requested attended transfer."), null);
        }

        var consultChannelId = CreateDeterministicAriId(
            AsteriskAriConstants.AttendedConsultChannelPrefix,
            string.Concat(request.InteractionId, "-", targetUserId));

        var context = new ResolvedConsultContext
        {
            CallerChannelId = callerChannelId,
            BridgeId = currentLeg.BridgeId,
            AgentChannelId = currentLeg.ChannelId,
            TargetUserId = targetUserId,
            ConsultChannelId = consultChannelId,
        };

        return (null, context);
    }

    private static ContactCenterVoiceProviderResult ConsultSuccess(string consultChannelId, string bridgeId)
    {
        return new ContactCenterVoiceProviderResult
        {
            Succeeded = true,
            ProviderName = AsteriskConstants.ProviderTechnicalName,
            Metadata = new Dictionary<string, string>
            {
                [AsteriskVoiceResultMetadata.AttendedTransferConsultChannelId] = consultChannelId,
                [AsteriskVoiceResultMetadata.AttendedTransferBridgeId] = bridgeId,
            },
        };
    }

    private async Task<bool> TryHoldAsync(string channelId, CancellationToken cancellationToken)
    {
        try
        {
            await _ariClient.HoldChannelAsync(channelId, cancellationToken);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Asterisk could not place the customer channel on hold for an attended transfer.");

            return false;
        }
    }

    private async Task<bool> TryUnholdAsync(string channelId, CancellationToken cancellationToken)
    {
        try
        {
            await _ariClient.UnholdChannelAsync(channelId, cancellationToken);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Asterisk could not resume the customer channel from hold for an attended transfer.");

            return false;
        }
    }

    private sealed class ResolvedConsultContext
    {
        public string CallerChannelId { get; init; }

        public string BridgeId { get; init; }

        public string AgentChannelId { get; init; }

        public string TargetUserId { get; init; }

        public string ConsultChannelId { get; init; }
    }
}
