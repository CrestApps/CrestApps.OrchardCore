using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// What connecting an agent to an accepted call writes to the audit log: the agent's leg answering, or the connect
/// failing and the offer the agent accepted being missed.
/// </summary>
public sealed partial class AnswerProviderCommandTypeExecutor
{
    private Task RecordAgentLegAnsweredAsync(
        CallSession session,
        Interaction interaction,
        string providerLegId,
        string agentId,
        DateTime answeredUtc,
        CancellationToken cancellationToken)
    {
        var data = ContactCenterCallAudit.ForSession(session, interaction);
        data.ProviderLegId = providerLegId;
        data.LegRole = nameof(CallPartyRole.Agent);
        data.AgentId = agentId;
        data.State = nameof(CallLegStatus.Answered);

        // Keyed on the leg alone, so the same answer reported again by the provider's own leg event is one record.
        return _auditRecorder.RecordCallAsync(
            ContactCenterConstants.Events.AgentLegAnswered,
            data,
            answeredUtc,
            ContactCenterActor.System,
            $"agent-leg:{ContactCenterConstants.Events.AgentLegAnswered}:{providerLegId}",
            cancellationToken);
    }

    private async Task RecordAnswerFailedAsync(
        ProviderCommand command,
        ProviderAnswerCommandRequest request,
        Interaction interaction,
        CallSession session,
        DateTime failedUtc,
        CancellationToken cancellationToken)
    {
        var data = session is not null
            ? ContactCenterCallAudit.ForSession(session, interaction)
            : interaction is not null
                ? ContactCenterCallAudit.ForInteraction(interaction)
                : new CallLifecycleEventData { InteractionId = command.InteractionId, ActivityItemId = command.ActivityItemId };

        data.InteractionId ??= command.InteractionId;
        data.ProviderCallId ??= request.ProviderCallId;
        data.LegRole = nameof(CallPartyRole.Agent);
        data.AgentId = request.AgentId;
        data.QueueId = request.QueueId;
        data.State = nameof(CallLegStatus.Failed);
        data.Reason = string.IsNullOrWhiteSpace(command.LastError)
            ? CallLifecycleReasons.AnswerFailed
            : command.LastError;

        await _auditRecorder.RecordCallAsync(
            ContactCenterConstants.Events.AgentLegFailed,
            data,
            failedUtc,
            ContactCenterActor.System,
            $"agent-leg:{ContactCenterConstants.Events.AgentLegFailed}:{command.CommandId}",
            cancellationToken);

        if (string.IsNullOrWhiteSpace(command.ReservationId))
        {
            return;
        }

        var reservation = await _reservationManager.FindByIdAsync(command.ReservationId, cancellationToken);

        if (reservation is null)
        {
            return;
        }

        var offer = ContactCenterCallAudit.ForOffer(reservation, interaction, agent: null, failedUtc, CallLifecycleReasons.AnswerFailed);
        offer.InteractionId ??= command.InteractionId;
        offer.UserId = request.AgentUserId;

        await _auditRecorder.RecordOfferAsync(
            ContactCenterConstants.Events.OfferMissed,
            offer,
            ContactCenterActor.System,
            cancellationToken);
    }
}
