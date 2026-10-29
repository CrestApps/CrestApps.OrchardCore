using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Coordinates the activity reservation lifecycle that locks a queue item for an agent before assignment.
/// </summary>
public interface IActivityReservationService
{
    /// <summary>
    /// Reserves a queue item for an agent and creates a short-lived reservation lock.
    /// </summary>
    /// <param name="queueItem">The queue item to reserve.</param>
    /// <param name="agent">The agent to reserve the item for.</param>
    /// <param name="timeoutSeconds">The number of seconds before the reservation expires.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The created reservation.</returns>
    Task<ActivityReservation> ReserveAsync(QueueItem queueItem, AgentProfile agent, int timeoutSeconds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Accepts a pending reservation and converts it into an assignment.
    /// </summary>
    /// <param name="reservationId">The reservation identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The accepted reservation, or <see langword="null"/> when not found or no longer pending.</returns>
    Task<ActivityReservation> AcceptAsync(string reservationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Accepts a pending reservation for a call a dialer is about to place for the agent, converting it into an
    /// assignment while holding the agent Reserved rather than putting them on a call.
    /// </summary>
    /// <remarks>
    /// The customer is dialed first and the agent is connected only once a person answers, so until then the agent is
    /// held for the call, not on it. <see cref="IAgentPresenceManager.ConnectHeldAgentAsync"/> puts them on it when
    /// they are connected; a call that ends without them releases them like any reservation that never reached them.
    /// </remarks>
    /// <param name="reservationId">The reservation identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The accepted reservation, or <see langword="null"/> when not found or no longer pending.</returns>
    Task<ActivityReservation> AcceptForDialAsync(string reservationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rejects a pending reservation and returns the item to its queue.
    /// </summary>
    /// <param name="reservationId">The reservation identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The rejected reservation, or <see langword="null"/> when not found or no longer pending.</returns>
    Task<ActivityReservation> RejectAsync(string reservationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rejects a pending reservation and sends its caller to voicemail instead of returning the item to its queue.
    /// </summary>
    /// <param name="reservationId">The reservation identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The rejected reservation, or <see langword="null"/> when not found or no longer pending.</returns>
    Task<ActivityReservation> RejectToVoicemailAsync(string reservationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a pending or accepted reservation and returns the item to its queue.
    /// </summary>
    /// <param name="reservationId">The reservation identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The canceled reservation, or <see langword="null"/> when not found or no longer active.</returns>
    Task<ActivityReservation> CancelAsync(string reservationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Compensates a pending or accepted reservation after its provider command cannot complete.
    /// </summary>
    /// <param name="reservationId">The reservation identifier.</param>
    /// <param name="removeFromQueue">Whether the owned queue item should be terminally removed.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The compensated reservation, or <see langword="null"/> when not found or no longer active.</returns>
    Task<ActivityReservation> CompensateAsync(
        string reservationId,
        bool removeFromQueue,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases an accepted reservation whose agent could not be reached: the agent took the offer, but the leg rung to
    /// their phone was refused or never answered, so the caller is still waiting with nobody on the way. The caller
    /// gets what the queue does with an offer nobody answers (its unanswered-offer action: back to the queue, to
    /// voicemail, or rejected), as an expired offer does, and the agent is set Away rather than returned to work, so
    /// the next caller is not offered to a phone that cannot ring.
    /// </summary>
    /// <remarks>
    /// Only an inbound caller in a regular queue is released this way. A direct-to-agent (personal line) reservation
    /// and a campaign queue's reservation are left alone, and so is a reservation that is not accepted: one still
    /// ringing rings out on its own, and one already settled was decided by whatever settled it, so a redelivered
    /// failure releases the caller once.
    /// </remarks>
    /// <param name="reservationId">The reservation identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The released reservation, or <see langword="null"/> when it was not found, is not an accepted
    /// reservation in a regular queue, or is held by another transition.</returns>
    /// <exception cref="YesSql.ConcurrencyException">Another transition changed the reservation, the agent or the
    /// call first.</exception>
    Task<ActivityReservation> ReleaseUnreachableAgentAsync(string reservationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases an offer that is still ringing when the agent's phone refused the leg rung for it as unavailable: no
    /// phone of theirs is there to ring, so the offer cannot be answered before it rings out. The caller is handed on
    /// at once, as an offer that rang out is (the queue's unanswered-offer action), and the agent is set Away, as when
    /// an accepted offer could not reach them, so the next caller is not offered to the same phone.
    /// </summary>
    /// <remarks>
    /// Only a pending offer to <paramref name="agentId"/> in a regular queue is released this way. A direct-to-agent
    /// (personal line) offer and a campaign queue's offer are left to ring out on their own, and an offer that is no
    /// longer pending, or now someone else's, was decided by whatever changed it.
    /// </remarks>
    /// <param name="reservationId">The reservation identifier.</param>
    /// <param name="agentId">The agent whose phone refused the leg.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The released reservation, or <see langword="null"/> when it was not found, is not that agent's pending
    /// offer in a regular queue, or is held by another transition.</returns>
    /// <exception cref="YesSql.ConcurrencyException">Another transition changed the reservation, the agent or the
    /// call first.</exception>
    Task<ActivityReservation> ReleaseUnreachableOfferAsync(string reservationId, string agentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Expires every pending reservation that has passed its timeout and returns items to their queues.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of reservations that expired.</returns>
    Task<int> ExpireDueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Claims an agent for an over-dialed call a person has just answered: under the activity and agent locks it
    /// re-checks that the call is still placed for the campaign with nobody claimed and that the agent is still free,
    /// then creates the reservation already accepted, names the agent on the queue item, the work state and the
    /// interaction, moves the agent to Busy and commits through the same compare-and-set as every other routing
    /// transition.
    /// </summary>
    /// <param name="queueItem">The call's queue item, Assigned with no agent.</param>
    /// <param name="agent">The agent to claim.</param>
    /// <param name="interactionId">The interaction of the answered call.</param>
    /// <param name="lockWait">How long to wait for the locks; a held agent is skipped rather than waited for.</param>
    /// <param name="beforeCommit">Work to stage in the same transaction once the agent is claimed, such as connecting
    /// the agent to the call.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The accepted reservation, or <see langword="null"/> when the call or the agent could not be claimed.</returns>
    /// <exception cref="YesSql.ConcurrencyException">Another transition changed the agent, the item or the call first.</exception>
    Task<ActivityReservation> ClaimConnectedCallAsync(
        QueueItem queueItem,
        AgentProfile agent,
        string interactionId,
        TimeSpan lockWait,
        Func<ActivityReservation, AgentProfile, Task> beforeCommit = null,
        CancellationToken cancellationToken = default);
}
