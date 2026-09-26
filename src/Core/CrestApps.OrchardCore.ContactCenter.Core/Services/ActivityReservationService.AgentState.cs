using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// How routing moves an agent when a reservation ends without its work being taken, through the one transition
/// point every agent state change is recorded by.
/// </summary>
public sealed partial class ActivityReservationService
{
    private async Task ReleaseAgentStateAsync(
        AgentProfile agent,
        ActivityReservation reservation,
        string releaseReason,
        string interactionId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var targetStatus = agent.RequestedPresenceStatus ?? AgentPresenceUtilities.ResolveDefaultReadyState(agent);

        // When the agent asked for a state while the offer rang (a break, say), the state they return to carries
        // the reason they gave for it.
        var requested = agent.RequestedPresenceStatus.HasValue && agent.PresenceRequestedUtc.HasValue;

        agent.RequestedPresenceStatus = null;
        agent.ActiveReservationId = null;

        await _stateTransitions.TransitionAsync(agent, targetStatus, new AgentStateChangeContext
        {
            Source = AgentStateChangeSources.Released,
            ReleaseReason = releaseReason,
            ReasonCodeId = requested ? agent.PresenceReasonCodeId : null,
            ReasonName = requested ? agent.PresenceReason : null,
            ReservationId = reservation.ItemId,
            InteractionId = interactionId,
            ChangedUtc = now,
        }, cancellationToken);
    }

    private async Task<string> FindInteractionIdAsync(string activityItemId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(activityItemId))
        {
            return null;
        }

        var interaction = await _interactionManager.FindByActivityIdAsync(activityItemId, cancellationToken);

        return interaction?.ItemId;
    }

    private static string ResolveReleaseReason(ReservationStatus status)
        => status switch
        {
            ReservationStatus.Expired => AgentReleaseReasons.Expired,
            ReservationStatus.Rejected => AgentReleaseReasons.Rejected,
            ReservationStatus.Canceled => AgentReleaseReasons.Canceled,
            _ => status.ToString(),
        };
}
