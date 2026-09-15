using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Orchestrates inbound voice calls. It turns a normalized <see cref="InboundVoiceEvent"/> into a CRM
/// activity and interaction, resolves the target queue and subject, and routes the call to an
/// available agent. Telephony remains responsible for the underlying media execution.
/// </summary>
public interface IInboundVoiceService
{
    /// <summary>
    /// Handles a normalized inbound voice event end to end: creates the interaction and CRM activity,
    /// enqueues the work, reserves an available agent, and offers the ringing call to that agent.
    /// </summary>
    /// <param name="inboundEvent">The normalized inbound voice event.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The routing outcome describing the created records and the offered agent.</returns>
    Task<InboundVoiceRoutingResult> HandleInboundAsync(InboundVoiceEvent inboundEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reserves the next available agent for the queue and offers the queued inbound call to that
    /// agent. Used to route a call initially and to re-offer it after an agent declines.
    /// </summary>
    /// <param name="queueId">The queue identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The identifier of the user the call was offered to, or <see langword="null"/> when no agent is available.</returns>
    Task<string> OfferNextAsync(string queueId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Offers a specific waiting call directly to a specific agent (a direct-to-agent / personal-line route).
    /// Used to re-offer a call that was held while the agent was unavailable once they become available.
    /// </summary>
    /// <param name="activityItemId">The activity whose held call is offered.</param>
    /// <param name="queueId">The queue the call is waiting in (the synthetic direct-routing queue).</param>
    /// <param name="agentId">The agent profile the call is offered to.</param>
    /// <param name="ringTimeoutSeconds">The ring window, in seconds, for the direct-to-agent offer. When null the direct-routing default is used.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The identifier of the user the call was offered to, or <see langword="null"/> when the agent is unavailable.</returns>
    Task<string> OfferToAgentAsync(
        string activityItemId,
        string queueId,
        string agentId,
        int? ringTimeoutSeconds = null,
        CancellationToken cancellationToken = default);
}
