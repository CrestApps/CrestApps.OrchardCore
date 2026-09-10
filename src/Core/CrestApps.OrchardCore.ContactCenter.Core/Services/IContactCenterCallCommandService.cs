using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Coordinates the agent-facing call commands for offered interactions as single, atomic, audited
/// operations. Accepting an offer accepts the reservation, connects the live media to the agent, and
/// advances the interaction and call session together so the orchestration state and the media state
/// can never diverge. This replaces uncoordinated, best-effort client actions.
/// </summary>
public interface IContactCenterCallCommandService
{
    /// <summary>
    /// Accepts an offered inbound interaction: accepts the reservation, connects (bridges) the live
    /// call to the agent for server-side ACD providers, and advances the interaction and call session
    /// to the connected state. For agent-device-native providers, the returned result indicates the
    /// agent's device must answer the media.
    /// </summary>
    /// <param name="reservationId">The reservation identifier of the offered interaction.</param>
    /// <param name="agentUserId">The Orchard user identifier of the agent accepting the offer.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The command result describing the outcome.</returns>
    Task<CallCommandResult> AcceptInboundOfferAsync(string reservationId, string agentUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Declines an offered inbound interaction: rejects the reservation, returns the work to its queue,
    /// and re-offers it to the next available agent.
    /// </summary>
    /// <param name="reservationId">The reservation identifier of the offered interaction.</param>
    /// <param name="agentUserId">The Orchard user identifier of the agent declining the offer.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The command result describing the outcome.</returns>
    Task<CallCommandResult> DeclineInboundOfferAsync(string reservationId, string agentUserId, CancellationToken cancellationToken = default);
}
