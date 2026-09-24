using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Holds the agent legs rung while their offers were still ringing. The state lives no longer than the offer, so it
/// is kept where every node can read it without a document write per step.
/// </summary>
public interface IAgentPreDialLegStore
{
    /// <summary>
    /// Finds the leg rung for an offer.
    /// </summary>
    /// <param name="reservationId">The offer (reservation) identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The leg, or <see langword="null"/> when the offer has none.</returns>
    Task<AgentPreDialLeg> FindAsync(string reservationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the offer whose leg was rung for an interaction.
    /// </summary>
    /// <param name="interactionId">The interaction identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The offer (reservation) identifier, or <see langword="null"/>.</returns>
    Task<string> FindReservationIdByInteractionAsync(string interactionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the offer whose leg was rung to an agent.
    /// </summary>
    /// <param name="agentId">The agent profile identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The offer (reservation) identifier, or <see langword="null"/>.</returns>
    Task<string> FindReservationIdByAgentAsync(string agentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the leg until the given time.
    /// </summary>
    /// <param name="leg">The leg.</param>
    /// <param name="expiresUtc">When the state may be forgotten.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task SaveAsync(AgentPreDialLeg leg, DateTime expiresUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Forgets the leg.
    /// </summary>
    /// <param name="leg">The leg.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task RemoveAsync(AgentPreDialLeg leg, CancellationToken cancellationToken = default);
}
