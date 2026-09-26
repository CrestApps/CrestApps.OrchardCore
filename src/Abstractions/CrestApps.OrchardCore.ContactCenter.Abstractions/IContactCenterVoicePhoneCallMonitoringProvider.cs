using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// A monitoring provider that can also put a supervisor on an agent's own phone call: a number the agent dialed from the
/// soft phone's keypad, or an internal extension call. Such a call is no Contact Center interaction, so the provider
/// first says how it is put together, and the engagement is then started, changed and stopped through
/// <see cref="IContactCenterVoiceMonitoringProvider"/> and <see cref="IContactCenterVoiceSupervisorInterventionProvider"/>
/// with that shape: <see cref="ContactCenterPhoneCallMonitoringTarget.ProviderCallId"/>,
/// <see cref="ContactCenterPhoneCallMonitoringTarget.AgentLegId"/>, and the conference named in the request's metadata
/// under <see cref="ContactCenterPhoneCallMonitoringTarget.ConferenceMetadataKey"/>.
/// </summary>
public interface IContactCenterVoicePhoneCallMonitoringProvider
{
    /// <summary>
    /// Whether a phone call of this provider could be monitored at all, from what the soft phone's call history says of
    /// it, without asking the provider: what the live dashboard offers.
    /// </summary>
    /// <param name="isExtension">Whether the call is an internal extension call.</param>
    /// <param name="isOutbound">Whether the agent the call is recorded for placed it.</param>
    /// <returns><see langword="true"/> when a supervisor may try to listen to it.</returns>
    bool CanMonitorPhoneCall(bool isExtension, bool isOutbound);

    /// <summary>
    /// Reads how a live phone call is put together, so a supervisor can be put on it.
    /// </summary>
    /// <param name="callId">The call as the soft phone's call history knows it: the leg of the agent who placed it.</param>
    /// <param name="isExtension">Whether the call is an internal extension call.</param>
    /// <param name="monitorsCallee">Whether the agent being monitored is the colleague the extension call rang, not the caller.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The call's shape, or <see langword="null"/> when it cannot be monitored (it ended, or it is not a call the provider can join).</returns>
    Task<ContactCenterPhoneCallMonitoringTarget> ResolvePhoneCallAsync(
        string callId,
        bool isExtension,
        bool monitorsCallee,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes a phone call over: the supervisor becomes a full party, the agent's leg is released, and the other party's
    /// end now ends the supervisor's leg rather than the agent's.
    /// </summary>
    /// <param name="request">The engagement, as for <see cref="IContactCenterVoiceSupervisorInterventionProvider.TakeOverAsync"/>.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The provider operation result.</returns>
    Task<ContactCenterVoiceProviderResult> TakeOverPhoneCallAsync(
        ContactCenterVoiceMonitoringRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Hangs up one leg of a phone call: the other party of a call a supervisor took over, when the supervisor hangs up.
    /// Hanging up a leg that is already gone is not an error.
    /// </summary>
    /// <param name="legId">The leg.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task HangupPhoneCallLegAsync(string legId, CancellationToken cancellationToken = default);
}
