using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Turns a normalized <see cref="InboundVoiceEvent"/> into Contact Center work: it creates the CRM
/// activity and interaction, resolves the target queue and subject, terminalizes closed or unroutable
/// calls, enqueues routable calls, and offers the ringing call to an available agent. Telephony remains
/// responsible for the underlying media execution.
/// </summary>
public interface IInboundVoiceCallProcessor
{
    /// <summary>
    /// Routes a normalized inbound voice event into Contact Center work, serializing concurrent routing of
    /// the same provider call by its provider call id.
    /// </summary>
    /// <param name="inboundEvent">The normalized inbound voice event.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The inbound routing result.</returns>
    Task<InboundVoiceRoutingResult> RouteInboundAsync(InboundVoiceEvent inboundEvent, CancellationToken cancellationToken = default);
}
