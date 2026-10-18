using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// An inbound caller a queue offered, whose agent accepted but could not be reached.
/// </summary>
/// <remarks>
/// Settling the call as failed, which suits a call the platform dialed, hung up on a caller who rang in and waited in
/// a queue, and left the agent whose phone could not ring Available to be offered the next caller. The caller instead
/// gets what the queue does with an offer nobody answers (back to the queue, or to voicemail), and the agent is set
/// Away. A busy phone (SIP 486) refuses the pre-dialed leg before the offer is accepted, so that offer rings out and
/// takes the same action on its own; this is the refusal that arrives after the accept (SIP 480, a registration that
/// has lapsed while the hub stayed connected).
/// </remarks>
public sealed partial class ContactCenterAgentLegFailureService
{
    /// <summary>
    /// Whether the call is an inbound caller a regular queue is handling, whose fate when the agent cannot be reached
    /// is the queue's unanswered-offer action rather than a failed call. A personal line has no queue to decide for the
    /// caller, and a campaign's call has its own abandonment handling.
    /// </summary>
    private static bool IsQueuedInboundCaller(Interaction interaction)
        => interaction.Direction == InteractionDirection.Inbound &&
            !string.IsNullOrEmpty(interaction.QueueId) &&
            !ContactCenterConstants.IsDirectRoutingQueue(interaction.QueueId) &&
            !ContactCenterConstants.IsCampaignQueue(interaction.QueueId);

    private async Task<bool> ReleaseQueuedCallerAsync(
        Interaction interaction,
        string providerName,
        string peerProviderCallId,
        HangupCause? hangupCause,
        CancellationToken cancellationToken)
    {
        var session = await _callSessionManager.FindByInteractionIdAsync(interaction.ItemId, cancellationToken);

        // Read before the legs are ended below: the agent leg still being connected is the one that failed.
        var agentLeg = session?.Legs.LastOrDefault(leg => leg.Role == CallPartyRole.Agent && !leg.EndedUtc.HasValue);
        var agentId = agentLeg?.AgentId ?? session?.AgentId ?? interaction.AgentId;

        var reservation = await FindAcceptedReservationAsync(interaction.ActivityItemId, agentId, cancellationToken);

        // The offer is no longer the agent's: an earlier delivery of this failure released the caller, the caller
        // hung up, or the offer was taken back. Whatever settled it decided for the caller, so nothing is written
        // here. A second release would send the caller to voicemail twice, and settling the call would hang up on a
        // caller who is waiting in the queue or leaving a message.
        if (reservation is null)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "The agent leg of queued inbound call '{ProviderCallId}' failed with cause {HangupCause}, but the call has no accepted offer left to release; the caller was already released.",
                    peerProviderCallId.SanitizeLogValue(),
                    hangupCause);
            }

            return false;
        }

        var now = _clock.UtcNow;
        var data = session is not null
            ? ContactCenterCallAudit.ForSession(session, interaction)
            : ContactCenterCallAudit.ForInteraction(interaction);
        data.ProviderLegId = agentLeg?.ProviderLegId;
        data.LegRole = nameof(CallPartyRole.Agent);
        data.AgentId = agentId ?? data.AgentId;
        data.State = nameof(CallLegStatus.Failed);
        data.HangupCause = hangupCause?.ToString();
        data.Reason = CallLifecycleReasons.AgentLegFailed;

        // The call goes on without the agent, so only the agent's legs end, and the call stops naming them. Written
        // here, before the release below commits: a committed unit of work no longer tracks what it loaded before, so
        // a session saved after it would be stored a second time.
        if (session is not null && !CallSessionLifecycle.IsTerminal(session.State))
        {
            foreach (var leg in session.Legs.Where(leg => leg.Role == CallPartyRole.Agent && !leg.EndedUtc.HasValue))
            {
                CallTopologyProjector.EndLeg(session, leg.ProviderLegId, now, hangupCause);
            }

            if (!string.IsNullOrEmpty(agentId) && string.Equals(session.AgentId, agentId, StringComparison.Ordinal))
            {
                session.AgentId = null;
            }

            await _callSessionManager.UpdateAsync(session, cancellationToken: cancellationToken);
        }

        await _auditRecorder.RecordCallAsync(
            ContactCenterConstants.Events.AgentLegFailed,
            data,
            now,
            ContactCenterActor.Provider(providerName),
            $"agent-leg:{ContactCenterConstants.Events.AgentLegFailed}:{agentLeg?.ProviderLegId ?? peerProviderCallId}",
            cancellationToken);

        var released = await _reservationService.ReleaseUnreachableAgentAsync(reservation.ItemId, cancellationToken);

        // Nothing read above is saved from here on. The release committed the unit of work, so the interaction and
        // the call session read before it are no longer tracked: saving either would store a second copy, or be
        // refused once what the release published has updated it in another scope.
        if (released is null)
        {
            _logger.LogWarning(
                "The agent leg of queued inbound call '{ProviderCallId}' failed with cause {HangupCause}, but accepted offer '{ReservationId}' was being settled by another transition; the caller was left to it.",
                peerProviderCallId.SanitizeLogValue(),
                hangupCause,
                reservation.ItemId.SanitizeLogValue());

            return false;
        }

        _logger.LogWarning(
            "The agent leg of queued inbound call '{ProviderCallId}' failed with cause {HangupCause} after agent '{AgentId}' accepted offer '{ReservationId}'; the caller follows the unanswered-offer action of queue '{QueueId}' and the agent was set away.",
            peerProviderCallId.SanitizeLogValue(),
            hangupCause,
            released.AgentId.SanitizeLogValue(),
            released.ItemId.SanitizeLogValue(),
            released.QueueId.SanitizeLogValue());

        return true;
    }

    /// <summary>
    /// Finds the accepted offer the caller is waiting on: the one made to the agent whose leg failed when that is
    /// known, else the newest.
    /// </summary>
    private async Task<ActivityReservation> FindAcceptedReservationAsync(
        string activityItemId,
        string agentId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(activityItemId))
        {
            return null;
        }

        var accepted = (await _reservationManager.GetActiveByActivityAsync(activityItemId, cancellationToken))
            .Where(reservation => reservation.Status == ReservationStatus.Accepted)
            .OrderByDescending(reservation => reservation.CreatedUtc);

        return accepted.FirstOrDefault(reservation => string.Equals(reservation.AgentId, agentId, StringComparison.Ordinal))
            ?? accepted.FirstOrDefault();
    }
}
