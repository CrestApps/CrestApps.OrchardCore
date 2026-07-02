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
}
