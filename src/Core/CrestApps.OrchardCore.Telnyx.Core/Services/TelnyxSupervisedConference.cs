using CrestApps.Core.Support;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Moves a Contact Center call between the two-leg bridge it normally runs on and the conference a supervisor listens
/// to it in, and back.
/// </summary>
/// <remarks>
/// <para>
/// A Contact Center agent's leg is bridged to the customer's with the bridge issued on the agent's leg and
/// <c>park_after_unbridge=self</c>. Creating a conference from the customer's leg moves the customer into it and parks
/// the agent's leg (the move a consult and a caller park already make); the agent's leg then joins, and the two talk
/// through the conference's mixer as they did through the bridge. Neither hears a tone: the conference is created and
/// joined with <c>beep_enabled=never</c>, and no hold audio is set, so the customer hears nothing for the moment they
/// are alone in it. The supervisor's leg joins with a Telnyx supervisor role.
/// </para>
/// <para>
/// The conference is named for the customer's leg, so every supervisor and every node finds the same one. When the
/// last supervisor leaves, the call is put back on a bridge exactly as it was, so everything the agent does afterwards
/// (hold, transfer, consult, park) runs on the topology it was written for.
/// </para>
/// </remarks>
internal sealed class TelnyxSupervisedConference
{
    private readonly TelnyxApiClient _apiClient;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxSupervisedConference"/> class.
    /// </summary>
    public TelnyxSupervisedConference(TelnyxApiClient apiClient, ILogger logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    /// <summary>
    /// The conference a supervised call runs in, named for its customer leg.
    /// </summary>
    /// <param name="customerLegId">The customer's leg.</param>
    /// <returns>The conference name.</returns>
    public static string ConferenceName(string customerLegId)
        => $"cc-sv-{customerLegId}";

    /// <summary>
    /// Whether a supervisor leg names the conference the platform moves a bridged call into for supervision (or names
    /// none), rather than a conference the call already runs in -- an extension call's own, which is joined as it is.
    /// </summary>
    /// <param name="conferenceName">The conference the supervisor's leg names.</param>
    /// <param name="customerLegId">The call's leg the supervised conference is named for.</param>
    /// <returns><see langword="true"/> when the call is to be moved into its supervised conference.</returns>
    public static bool IsOwnConference(string conferenceName, string customerLegId)
        => string.IsNullOrWhiteSpace(conferenceName) ||
            string.Equals(conferenceName.Trim(), ConferenceName(customerLegId), StringComparison.Ordinal);

    /// <summary>
    /// Finds a conference a call already runs in, without making one or moving anybody: an extension call's own
    /// conference was made from the caller's leg, and a conference made from any other leg would bind that leg to it.
    /// </summary>
    /// <param name="conferenceName">The conference.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The conference id, or <see langword="null"/> when it is not running.</returns>
    public async Task<string> FindRunningAsync(string conferenceName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(conferenceName))
        {
            return null;
        }

        var conference = await _apiClient.FindLiveConferenceByNameAsync(conferenceName.Trim(), cancellationToken);

        return string.IsNullOrWhiteSpace(conference.ConferenceId) ? null : conference.ConferenceId;
    }

    /// <summary>
    /// The Telnyx supervisor role for a Contact Center monitoring mode name.
    /// </summary>
    /// <param name="mode">The mode name: Monitor, Whisper or Barge.</param>
    /// <returns>The Telnyx role.</returns>
    public static string RoleFor(string mode)
        => mode?.Trim().ToLowerInvariant() switch
        {
            "whisper" => "whisper",
            "barge" => "barge",
            _ => "monitor",
        };

    /// <summary>
    /// The legs a supervisor in <paramref name="role"/> is heard by: the agent alone for a whisper, nobody otherwise.
    /// </summary>
    public static IReadOnlyCollection<string> WhisperTargets(string role, string agentLegId)
        => string.Equals(role, "whisper", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(agentLegId)
            ? [agentLegId]
            : null;

    /// <summary>
    /// Returns the running conference of the call, moving the call into one first when it is still on its bridge.
    /// </summary>
    /// <param name="customerLegId">The customer's leg.</param>
    /// <param name="agentLegId">The agent's leg bridged to it.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The conference id, or <see langword="null"/> when the call could not be moved, in which case it is left on its bridge.</returns>
    public async Task<string> EnsureAsync(string customerLegId, string agentLegId, CancellationToken cancellationToken)
    {
        var name = ConferenceName(customerLegId);
        var existing = await _apiClient.FindLiveConferenceByNameAsync(name, cancellationToken);

        if (!string.IsNullOrWhiteSpace(existing.ConferenceId))
        {
            return existing.ConferenceId;
        }

        if (string.IsNullOrWhiteSpace(agentLegId))
        {
            _logger.LogWarning(
                "The call '{CustomerLegId}' cannot be supervised: its agent leg is not known.",
                customerLegId.SanitizeLogValue());

            return null;
        }

        var created = await _apiClient.CreateSilentConferenceAsync(name, customerLegId, commandId: name, cancellationToken);

        if (!created.Succeeded || string.IsNullOrWhiteSpace(created.ConferenceId))
        {
            // Another supervisor's leg may have answered at the same moment and made it first.
            var raced = await _apiClient.FindLiveConferenceByNameAsync(name, cancellationToken);

            if (!string.IsNullOrWhiteSpace(raced.ConferenceId))
            {
                return raced.ConferenceId;
            }

            _logger.LogError(
                "Telnyx refused to move call '{CustomerLegId}' into a supervised conference with status code {StatusCode}. Response: {Response}",
                customerLegId.SanitizeLogValue(),
                created.StatusCode,
                created.ErrorBody.SanitizeLogValue());

            return null;
        }

        var joined = await _apiClient.JoinConferenceSilentlyAsync(
            created.ConferenceId,
            agentLegId,
            commandId: $"{name}-agent-{agentLegId}",
            cancellationToken: cancellationToken);

        if (!joined.Succeeded && !TelnyxApiErrors.IsAlreadyInConference(joined))
        {
            _logger.LogError(
                "Telnyx refused to join agent leg '{AgentLegId}' to the supervised conference of call '{CustomerLegId}' with status code {StatusCode}; the call is put back on its bridge. Response: {Response}",
                agentLegId.SanitizeLogValue(),
                customerLegId.SanitizeLogValue(),
                joined.StatusCode,
                joined.ErrorBody.SanitizeLogValue());

            await BridgeBackAsync(created.ConferenceId, customerLegId, agentLegId, cancellationToken);

            return null;
        }

        return created.ConferenceId;
    }

    /// <summary>
    /// Joins a supervisor's leg to the call's conference in a role.
    /// </summary>
    /// <remarks>
    /// Listening is never Telnyx's <c>monitor</c> supervisor role. Live, every conference a supervisor joined as
    /// <c>monitor</c> stopped carrying the customer's and the agent's audio to each other (the agent's phone kept receiving
    /// packets at an inbound level of 0.002, against 0.4 in the same call's conference joined as <c>whisper</c>), and a
    /// later change of role did not bring it back. A listening supervisor joins as an ordinary participant, muted: they
    /// hear everybody and nobody hears them.
    /// </remarks>
    public async Task<bool> JoinSupervisorAsync(
        string conferenceId,
        string supervisorLegId,
        string role,
        string agentLegId,
        CancellationToken cancellationToken)
    {
        var listening = IsListening(role);
        var joined = await _apiClient.JoinConferenceSilentlyAsync(
            conferenceId,
            supervisorLegId,
            listening ? null : role,
            WhisperTargets(role, agentLegId),
            commandId: $"cc-sv-join-{supervisorLegId}",
            mute: listening,
            cancellationToken: cancellationToken);

        if (joined.Succeeded || TelnyxApiErrors.IsAlreadyInConference(joined))
        {
            return true;
        }

        _logger.LogError(
            "Telnyx refused to join supervisor leg '{SupervisorLegId}' as {Role} with status code {StatusCode}. Response: {Response}",
            supervisorLegId.SanitizeLogValue(),
            role.SanitizeLogValue(),
            joined.StatusCode,
            joined.ErrorBody.SanitizeLogValue());

        return false;
    }

    /// <summary>
    /// Changes a joined supervisor's role.
    /// </summary>
    /// <returns><see langword="true"/> when Telnyx changed it; <see langword="false"/> when the call has no running conference or the supervisor is not in it yet.</returns>
    public async Task<bool> SwitchRoleAsync(
        string conferenceName,
        string supervisorLegId,
        string role,
        string agentLegId,
        CancellationToken cancellationToken)
    {
        var conference = await _apiClient.FindLiveConferenceByNameAsync(conferenceName, cancellationToken);

        if (string.IsNullOrWhiteSpace(conference.ConferenceId))
        {
            return false;
        }

        TelnyxApiResult updated;

        if (IsListening(role))
        {
            // Muted before the role goes: a whisperer made an ordinary participant first would be heard by the customer
            // for a moment.
            updated = await _apiClient.SetConferenceParticipantMutedAsync(conference.ConferenceId, supervisorLegId, mute: true, cancellationToken);

            if (updated.Succeeded)
            {
                updated = await _apiClient.UpdateConferenceSupervisorRoleAsync(conference.ConferenceId, supervisorLegId, "none", whisperCallControlIds: null, cancellationToken);
            }
        }
        else
        {
            // The role first, so a listener made to whisper is never heard by the customer on the way.
            updated = await _apiClient.UpdateConferenceSupervisorRoleAsync(
                conference.ConferenceId,
                supervisorLegId,
                role,
                WhisperTargets(role, agentLegId),
                cancellationToken);

            if (updated.Succeeded)
            {
                updated = await _apiClient.SetConferenceParticipantMutedAsync(conference.ConferenceId, supervisorLegId, mute: false, cancellationToken);
            }
        }

        if (!updated.Succeeded && _logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Telnyx returned {StatusCode} changing supervisor leg '{SupervisorLegId}' to {Role}. Response: {Response}",
                updated.StatusCode,
                supervisorLegId.SanitizeLogValue(),
                role.SanitizeLogValue(),
                updated.ErrorBody.SanitizeLogValue());
        }

        return updated.Succeeded;
    }

    // Whether a Contact Center mode's Telnyx role is listening only.
    private static bool IsListening(string role)
        => string.IsNullOrWhiteSpace(role) || string.Equals(role, "monitor", StringComparison.Ordinal);

    /// <summary>
    /// Puts the call back on a bridge once no supervisor is left in its conference.
    /// </summary>
    /// <param name="customerLegId">The customer's leg.</param>
    /// <param name="agentLegId">The agent's leg, when known; otherwise the one participant that is not a supervisor is used.</param>
    /// <param name="leavingSupervisorLegId">A supervisor leg that is leaving and is not counted, even if Telnyx still lists it.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the call was put back on its bridge.</returns>
    public async Task<bool> RestoreIfUnsupervisedAsync(
        string customerLegId,
        string agentLegId,
        string leavingSupervisorLegId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(customerLegId))
        {
            return false;
        }

        var conference = await _apiClient.FindLiveConferenceByNameAsync(ConferenceName(customerLegId), cancellationToken);

        if (string.IsNullOrWhiteSpace(conference.ConferenceId))
        {
            return false;
        }

        var participants = await _apiClient.ListJoinedConferenceParticipantsAsync(conference.ConferenceId, cancellationToken);

        if (participants is null || !participants.Contains(customerLegId, StringComparer.Ordinal))
        {
            return false;
        }

        string agentParticipant = null;

        foreach (var participant in participants)
        {
            if (string.Equals(participant, customerLegId, StringComparison.Ordinal) ||
                string.Equals(participant, leavingSupervisorLegId, StringComparison.Ordinal))
            {
                continue;
            }

            var status = await _apiClient.GetCallStatusAsync(participant, cancellationToken);

            if (status.Succeeded &&
                TelnyxOutboundBridgeState.TryParseEncoded(status.ClientState, out var state) &&
                state.Intent == TelnyxOutboundBridgeState.ContactCenterSupervisorLegIntent)
            {
                if (state.Detached != true)
                {
                    // Somebody is still listening: the call stays where they can hear it.
                    return false;
                }

                continue;
            }

            if (string.IsNullOrWhiteSpace(agentLegId) || string.Equals(participant, agentLegId, StringComparison.Ordinal))
            {
                agentParticipant = participant;
            }
        }

        if (string.IsNullOrWhiteSpace(agentParticipant))
        {
            // The agent is gone (a takeover handed the call to the supervisor, who has since left): there is nobody to
            // bridge the customer back to.
            return false;
        }

        return await BridgeBackAsync(conference.ConferenceId, customerLegId, agentParticipant, cancellationToken);
    }

    // Takes both parties out of the conference, which parks them, and bridges them again the way the Contact Center
    // joined them: on the agent's leg, with park_after_unbridge=self.
    private async Task<bool> BridgeBackAsync(string conferenceId, string customerLegId, string agentLegId, CancellationToken cancellationToken)
    {
        await _apiClient.LeaveConferenceAsync(conferenceId, customerLegId, cancellationToken);
        await _apiClient.LeaveConferenceAsync(conferenceId, agentLegId, cancellationToken);

        var bridged = await _apiClient.PostCallActionAsync(agentLegId, "bridge", new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["call_control_id"] = customerLegId,
            ["park_after_unbridge"] = "self",
        }, cancellationToken);

        if (!bridged.Succeeded)
        {
            _logger.LogError(
                "Telnyx refused to bridge agent leg '{AgentLegId}' back to call '{CustomerLegId}' after supervision with status code {StatusCode}. Response: {Response}",
                agentLegId.SanitizeLogValue(),
                customerLegId.SanitizeLogValue(),
                bridged.StatusCode,
                bridged.ErrorBody.SanitizeLogValue());

            return false;
        }

        return true;
    }
}
