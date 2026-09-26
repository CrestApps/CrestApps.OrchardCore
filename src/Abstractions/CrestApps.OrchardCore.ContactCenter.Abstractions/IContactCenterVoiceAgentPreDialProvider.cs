using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// A server-side ACD voice provider that can ring the agent's own device while an offer is still ringing, so the
/// agent's leg is already up when they accept and only has to be joined to the caller.
/// </summary>
/// <remarks>
/// <para>
/// Dialing the agent after they click Answer made them wait for the whole invite-and-answer round trip of their own
/// device, on top of the accept. Pre-dialing moves that round trip into the time the offer spends ringing, which the
/// agent spends deciding anyway.
/// </para>
/// <para>
/// The Contact Center owns every decision here: it pre-dials when an offer is presented, joins the leg to the caller
/// only once the offer is accepted and the leg is answered, and hangs the leg up when the offer ends any other way. A
/// provider only executes those three operations. A provider that cannot pre-dial (or refuses to for a given agent)
/// fails <see cref="PreDialAgentAsync"/>, and the accept connects the agent the ordinary way.
/// </para>
/// </remarks>
public interface IContactCenterVoiceAgentPreDialProvider
{
    /// <summary>
    /// Originates a leg to the agent's device for an offer that is still ringing. The leg must be correlatable to the
    /// offer by the agent's client, which holds it unanswered until the agent accepts.
    /// </summary>
    /// <param name="request">The offer the leg belongs to.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A successful result carrying the new leg in <see cref="ContactCenterVoiceProviderResult.ProviderLegId"/>, or a
    /// failed one when the agent cannot be pre-dialed.
    /// </returns>
    Task<ContactCenterVoiceProviderResult> PreDialAgentAsync(
        ContactCenterAgentPreDialRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Joins a pre-dialed agent leg that has been answered to the caller's leg, and stops whatever the caller was
    /// hearing while they waited.
    /// </summary>
    /// <param name="providerCallId">The provider's identifier for the caller's leg.</param>
    /// <param name="agentLegId">The provider's identifier for the pre-dialed agent leg.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The provider operation result.</returns>
    Task<ContactCenterVoiceProviderResult> BridgePreDialedAgentAsync(
        string providerCallId,
        string agentLegId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Hangs up a pre-dialed agent leg whose offer ended without being accepted. A leg that has already gone is not
    /// an error.
    /// </summary>
    /// <param name="agentLegId">The provider's identifier for the pre-dialed agent leg.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task HangupPreDialedAgentAsync(string agentLegId, CancellationToken cancellationToken = default);
}
