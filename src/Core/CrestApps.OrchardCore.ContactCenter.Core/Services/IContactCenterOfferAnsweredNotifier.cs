using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Tells every client an agent has open that one of them has just answered an offer, from inside the accept itself.
/// </summary>
/// <remarks>
/// The durable offer events reach clients through the outbox, about a second after the accept commits. For that
/// second every other page, tab and device the agent had open kept ringing in the headset for a call that had
/// already been answered. The durable events still follow; this only closes the gap, so an implementation is best
/// effort and must never fail the accept.
/// </remarks>
public interface IContactCenterOfferAnsweredNotifier
{
    /// <summary>
    /// Announces that the reservation was accepted by its agent.
    /// </summary>
    /// <param name="reservation">The accepted reservation.</param>
    /// <param name="agentUserId">The Orchard user the reservation's agent profile represents.</param>
    /// <param name="providerCallId">The provider call identifier the offer rang the agent with, when it is a call.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task NotifyAnsweredAsync(
        ActivityReservation reservation,
        string agentUserId,
        string providerCallId,
        CancellationToken cancellationToken = default);
}
