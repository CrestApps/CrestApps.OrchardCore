using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using OrchardCore;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Registers the command that connects an agent's leg to an answered outbound call, for the agent reserved when the call
/// was placed and for the agent claimed when an over-dialed call is answered alike.
/// </summary>
internal static class AnsweredCallBridge
{
    /// <summary>
    /// Registers the Answer command that bridges the agent to the call. It is dispatched by the caller once the
    /// transaction that registered it commits.
    /// </summary>
    /// <param name="commandStateService">The durable provider-command service.</param>
    /// <param name="callSessionManager">The call session manager, to keep the command id on the session.</param>
    /// <param name="session">The answered call.</param>
    /// <param name="interaction">The interaction of the call.</param>
    /// <param name="agentId">The agent to connect.</param>
    /// <param name="agentUserId">The agent's user, which call-control authorization is keyed on.</param>
    /// <param name="reservationId">The reservation the agent holds the call under, when there is one.</param>
    /// <param name="standbyReservationId">
    /// The reservation to tag the agent's leg with, so a phone standing by answers it at once; only for an agent claimed
    /// without an offer.
    /// </param>
    /// <param name="agentLegTimeoutSeconds">How long the agent's leg may ring, or 0 for the provider's default.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The command id, to dispatch after commit.</returns>
    public static async Task<string> RegisterAsync(
        IProviderCommandStateService commandStateService,
        ICallSessionManager callSessionManager,
        CallSession session,
        Interaction interaction,
        string agentId,
        string agentUserId,
        string reservationId,
        string standbyReservationId,
        int agentLegTimeoutSeconds,
        CancellationToken cancellationToken)
    {
        if (!session.Metadata.TryGetValue(ContactCenterConstants.CommandMetadata.CommandId, out var commandId) ||
            string.IsNullOrEmpty(commandId))
        {
            commandId = IdGenerator.GenerateId();
            session.Metadata[ContactCenterConstants.CommandMetadata.CommandId] = commandId;
            await callSessionManager.UpdateAsync(session, cancellationToken: cancellationToken);
        }

        await commandStateService.RegisterAsync(new ProviderCommandRegistration
        {
            CommandId = commandId,
            ProviderName = session.ProviderName,
            CommandType = ProviderCommandType.Answer,
            ActivityItemId = interaction.ActivityItemId,
            InteractionId = interaction.ItemId,
            ReservationId = reservationId,
            RemoveReservationFromQueueOnFailure = false,
            RequestPayload = JsonSerializer.Serialize(new ProviderAnswerCommandRequest
            {
                ActivityId = interaction.ActivityItemId,
                InteractionId = interaction.ItemId,
                ProviderCallId = session.ProviderCallId,
                AgentId = agentId,
                AgentUserId = agentUserId,
                QueueId = session.QueueId ?? interaction.QueueId,
                StandbyReservationId = standbyReservationId,
                AgentLegTimeoutSeconds = agentLegTimeoutSeconds,
            }),
        }, cancellationToken);

        return commandId;
    }
}
