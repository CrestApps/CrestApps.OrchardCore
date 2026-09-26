using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// A supervisor on an agent's own phone call: a number dialed from the soft phone's keypad, or an extension call.
/// </summary>
/// <remarks>
/// <para>
/// A number dialed from the keypad runs on the server exactly as a Contact Center call does: the agent's leg is bridged
/// to the number's leg, with the bridge issued on the agent's leg and <c>park_after_unbridge=self</c>, and the agent's
/// leg names the number's leg in its client state. So it is supervised the same way: the number's leg is moved into a
/// conference of its own (<c>cc-sv-{number leg}</c>), the agent's leg joins it, and when the last supervisor leaves both
/// leave it and are bridged again. The number's leg made the conference, and leaving it frees the leg, so it outlives
/// the conference ending.
/// </para>
/// <para>
/// An extension call already runs in a conference, <c>ext-{caller leg}</c>, made from the caller's own leg, with the
/// colleague joined to it. The supervisor joins that conference as it is -- no new conference is made, and no leg is
/// moved, so no leg is ever bound to a conference it has to outlive -- and stopping only hangs the supervisor's leg up.
/// It cannot be taken over: each colleague's end hangs the other up, and the conference is the caller's.
/// </para>
/// </remarks>
public sealed partial class TelnyxContactCenterVoiceProvider : IContactCenterVoicePhoneCallMonitoringProvider
{
    /// <inheritdoc/>
    public bool CanMonitorPhoneCall(bool isExtension, bool isOutbound)
        => isExtension || isOutbound;

    /// <inheritdoc/>
    public async Task<ContactCenterPhoneCallMonitoringTarget> ResolvePhoneCallAsync(
        string callId,
        bool isExtension,
        bool monitorsCallee,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(callId) || !_options.IsConfigured)
        {
            return null;
        }

        var agentLegId = callId.Trim();
        var status = await _apiClient.GetCallStatusAsync(agentLegId, cancellationToken);

        if (!status.Succeeded ||
            !status.IsAlive ||
            !TelnyxOutboundBridgeState.TryParseEncoded(status.ClientState, out var state) ||
            state.Detached == true)
        {
            return null;
        }

        if (isExtension)
        {
            if (!state.IsExtensionAgentLeg)
            {
                return null;
            }

            var conferenceName = TelnyxTelephonyProvider.ExtensionConferenceName(agentLegId);
            var conference = await _apiClient.FindLiveConferenceByNameAsync(conferenceName, cancellationToken);

            if (string.IsNullOrWhiteSpace(conference.ConferenceId))
            {
                // Not connected yet, or merged elsewhere: there is no conference to join.
                return null;
            }

            return new ContactCenterPhoneCallMonitoringTarget
            {
                ProviderCallId = agentLegId,
                AgentLegId = monitorsCallee ? state.PeerCallControlId : agentLegId,
                OtherPartyLegId = monitorsCallee ? agentLegId : state.PeerCallControlId,
                ConferenceName = conferenceName,
            };
        }

        // Still ringing: there is nobody to listen to yet, and nothing to bridge back to.
        if (!state.IsBridgedDialAgentLeg || state.PeerAnswered == false)
        {
            return null;
        }

        var party = await _apiClient.GetCallStatusAsync(state.PeerCallControlId, cancellationToken);

        if (!party.Succeeded || !party.IsAlive)
        {
            return null;
        }

        return new ContactCenterPhoneCallMonitoringTarget
        {
            ProviderCallId = state.PeerCallControlId,
            AgentLegId = agentLegId,
            OtherPartyLegId = state.PeerCallControlId,
            ConferenceName = TelnyxSupervisedConference.ConferenceName(state.PeerCallControlId),
            CanTakeOver = true,
        };
    }

    /// <inheritdoc/>
    public async Task<ContactCenterVoiceProviderResult> TakeOverPhoneCallAsync(
        ContactCenterVoiceMonitoringRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // A number dialed from the keypad runs like a Contact Center call, and is taken over the same way: the number's leg
        // is handed to the leg the supervisor takes it on, so the number hanging up ends the supervisor's call rather than
        // leaving them alone.
        return await TakeOverAsync(request, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task HangupPhoneCallLegAsync(string legId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(legId))
        {
            return;
        }

        var result = await _apiClient.HangupAsync(legId.Trim(), cancellationToken);

        if (!result.Succeeded && !TelnyxApiErrors.IsCallAlreadyEnded(result) && _logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Telnyx returned {StatusCode} hanging up leg '{LegId}' of a phone call a supervisor had taken over.",
                result.StatusCode,
                legId.SanitizeLogValue());
        }
    }

    // The conference a supervisor joins: the one the phone call runs in when the request names it, otherwise the one a
    // Contact Center call is moved into.
    private static string SupervisedConferenceName(ContactCenterVoiceMonitoringRequest request)
        => request.Metadata is not null &&
            request.Metadata.TryGetValue(ContactCenterPhoneCallMonitoringTarget.ConferenceMetadataKey, out var name) &&
            !string.IsNullOrWhiteSpace(name)
            ? name.Trim()
            : TelnyxSupervisedConference.ConferenceName(request.ProviderCallId?.Trim());
}
