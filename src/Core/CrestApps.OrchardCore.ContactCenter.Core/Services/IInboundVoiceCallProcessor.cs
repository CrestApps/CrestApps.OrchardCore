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

    /// <summary>
    /// Times out a held direct-to-agent (personal line) call that has waited past its ring window: removes it
    /// from the synthetic direct-routing queue and sends the caller to the target agent's voicemail. Only
    /// affects a still-waiting held call; once offered/reserved the reservation timeout governs instead.
    /// </summary>
    /// <param name="activityItemId">The activity whose held call is timed out.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when a held call was timed out; otherwise <see langword="false"/>.</returns>
    Task<bool> TimeoutDirectHoldAsync(string activityItemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a caller who is still waiting in a queue to voicemail: removes the item from its queue and ends
    /// the inbound call through the provider's voicemail path. Only affects a still-waiting voice call; once
    /// offered or reserved, the reservation owns the call and this returns <see langword="false"/>.
    /// </summary>
    /// <param name="activityItemId">The activity whose waiting call is sent to voicemail.</param>
    /// <param name="reasonCode">The reason recorded against the activity and interaction.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the caller was sent to voicemail; otherwise <see langword="false"/>.</returns>
    Task<bool> SendWaitingToVoicemailAsync(string activityItemId, string reasonCode, CancellationToken cancellationToken = default);
}
