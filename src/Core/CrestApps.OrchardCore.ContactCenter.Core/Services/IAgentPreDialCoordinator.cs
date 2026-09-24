using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Rings an agent's device while their voice offer is still ringing, and decides what becomes of that leg: joined to
/// the caller once the offer is accepted and the leg answered, or hung up when the offer ends any other way.
/// </summary>
/// <remarks>
/// Every transition for one offer is serialized on a lock of its own, and the leg is joined exactly once, by
/// whichever of "accepted and caller ready" and "agent answered" arrives second. A leg whose offer was declined,
/// expired, revoked, given to someone else, or abandoned by the caller is never joined; it is hung up.
/// </remarks>
public interface IAgentPreDialCoordinator
{
    /// <summary>
    /// Rings the agent's device for a pending voice offer, when the provider and the agent's client can hold such a
    /// leg. Doing nothing is always safe: the accept then connects the agent the ordinary way.
    /// </summary>
    /// <param name="reservationId">The offer (reservation) identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the offer has a pre-dialed leg.</returns>
    Task<bool> PreDialAsync(string reservationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the pre-dialed leg an accept should join, waiting for a pre-dial still in flight so the accept never
    /// rings the agent a second time.
    /// </summary>
    /// <param name="reservationId">The offer (reservation) identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The leg, or <see langword="null"/> when the offer has none to join.</returns>
    Task<AgentPreDialLeg> GetForAcceptAsync(string reservationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that the accepted offer's caller leg is ready, and joins the agent leg when it is already answered.
    /// </summary>
    /// <param name="reservationId">The offer (reservation) identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the legs are now joined.</returns>
    Task<bool> OnCallerReadyAsync(string reservationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that the agent's device answered a pre-dialed leg, and joins it to the caller when the offer is
    /// accepted and the caller ready. A leg whose offer is no longer this agent's to take is hung up.
    /// </summary>
    /// <param name="providerName">The technical name of the provider that reported the answer.</param>
    /// <param name="reservationId">The offer (reservation) identifier the leg carries.</param>
    /// <param name="agentLegId">The provider's identifier for the leg.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task OnAgentLegAnsweredAsync(string providerName, string reservationId, string agentLegId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that a pre-dialed leg ended. A leg that ended after its offer was accepted but before it was joined
    /// fails the call the same way an agent leg that could not be reached does.
    /// </summary>
    /// <param name="providerName">The technical name of the provider that reported the end.</param>
    /// <param name="reservationId">The offer (reservation) identifier the leg carries.</param>
    /// <param name="agentLegId">The provider's identifier for the leg.</param>
    /// <param name="cause">Why the leg ended, when the provider said.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task OnAgentLegEndedAsync(string providerName, string reservationId, string agentLegId, HangupCause? cause, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hangs up the offer's pre-dialed leg unless it was already joined to the caller.
    /// </summary>
    /// <param name="reservationId">The offer (reservation) identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task ReleaseAsync(string reservationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hangs up the pre-dialed leg rung for an interaction's offer unless it was already joined to the caller.
    /// </summary>
    /// <param name="interactionId">The interaction identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task ReleaseForInteractionAsync(string interactionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hangs up the pre-dialed leg rung to an agent unless it was already joined to the caller.
    /// </summary>
    /// <param name="agentId">The agent profile identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task ReleaseForAgentAsync(string agentId, CancellationToken cancellationToken = default);
}
