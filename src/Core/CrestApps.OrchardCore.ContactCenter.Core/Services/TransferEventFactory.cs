using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Builds the transfer events every transfer path records, so a blind transfer, a warm handover and a refused request
/// all read the same way in the audit trail and the transfer reports.
/// </summary>
internal static class TransferEventFactory
{
    public static InteractionEvent Transferred(
        Interaction interaction,
        string agentId,
        string userId,
        InteractionTransferType transferType,
        InteractionTransferTargetType targetType,
        string target,
        string reason,
        DateTime occurredUtc)
    {
        var interactionEvent = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.InteractionTransferred,
            InteractionId = interaction.ItemId,
            AggregateType = nameof(Interaction),
            AggregateId = interaction.ItemId,
            ActorId = string.IsNullOrEmpty(userId) ? agentId : userId,
            ActorType = ContactCenterActorType.Agent,
            SourceComponent = ContactCenterConstants.Components.Interactions,
            OccurredUtc = occurredUtc,
        };

        // Where the call went is what a transfer report needs and what the event alone did not say.
        var data = ContactCenterCallAudit.ForInteraction(interaction);
        data.AgentId = agentId ?? data.AgentId;
        data.Target = target;
        data.Reason = reason;
        data.Details["transferType"] = transferType.ToString();
        data.Details["targetType"] = targetType.ToString();

        if (!string.IsNullOrEmpty(userId))
        {
            data.Details["transferredByUserId"] = userId;
        }

        interactionEvent.SetData(data);

        return interactionEvent;
    }

    public static InteractionEvent Denied(
        Interaction interaction,
        string agentId,
        string userId,
        InteractionTransferTargetType targetType,
        string reason,
        DateTime occurredUtc)
    {
        var interactionEvent = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.InteractionTransferDenied,
            InteractionId = interaction.ItemId,
            AggregateType = nameof(Interaction),
            AggregateId = interaction.ItemId,
            ActorId = string.IsNullOrEmpty(userId) ? agentId ?? interaction.AgentId : userId,
            ActorType = ContactCenterActorType.Agent,
            SourceComponent = ContactCenterConstants.Components.Interactions,
            OccurredUtc = occurredUtc,
        };

        interactionEvent.SetData(new Dictionary<string, string>
        {
            ["targetType"] = targetType.ToString(),
            ["reason"] = reason ?? string.Empty,
        });

        return interactionEvent;
    }
}
