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
/// are alone in it.
/// </para>
/// <para>
/// The supervisor always joins as a Telnyx <c>whisper</c> supervisor naming the legs that hear them: the agent while they
/// listen (their phone keeps its microphone off, so the agent hears nothing) or coach, and everybody else on the call when
/// they join it. Nobody is ever muted in the conference and the <c>monitor</c> role is never used: live, a conference a
/// supervisor joined muted, or as <c>monitor</c>, stopped carrying anybody's audio to anybody -- the customer and the agent
/// could no longer hear each other -- and changing the supervisor afterwards did not bring it back. Every join and change
/// is read back from Telnyx and logged, and a change Telnyx answered but did not apply is made again by rejoining.
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
    /// Whether a supervisor in <paramref name="role"/> is heard by everybody on the call rather than the agent alone.
    /// </summary>
    /// <param name="role">The mode's Telnyx role name, as <see cref="RoleFor"/> gives it.</param>
    public static bool IsHeardByEverybody(string role)
        => string.Equals(role, "barge", StringComparison.Ordinal);

    /// <summary>
    /// The legs a supervisor in <paramref name="role"/> is heard by: everybody else on the call when they join it, the
    /// agent alone otherwise -- coaching, or listening with the phone's microphone off.
    /// </summary>
    /// <param name="role">The mode's Telnyx role name.</param>
    /// <param name="agentLegId">The monitored agent's leg.</param>
    /// <param name="participants">The legs in the conference.</param>
    /// <param name="supervisorLegId">The supervisor's own leg, which never hears itself.</param>
    public static IReadOnlyList<string> HearersFor(
        string role,
        string agentLegId,
        IEnumerable<string> participants,
        string supervisorLegId)
    {
        if (!IsHeardByEverybody(role) && !string.IsNullOrWhiteSpace(agentLegId))
        {
            return [agentLegId];
        }

        return (participants ?? [])
            .Where(participant => !string.IsNullOrWhiteSpace(participant) && !string.Equals(participant, supervisorLegId, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

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

        // Telnyx ignores a command whose id it has already executed, and the name repeats for every engagement on the
        // call: each attempt has an id of its own.
        var attempt = Guid.NewGuid().ToString("N");
        var created = await _apiClient.CreateSilentConferenceAsync(name, customerLegId, commandId: $"{name}-{attempt}", cancellationToken);

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
            commandId: $"{name}-agent-{attempt}",
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
    /// Joins a supervisor's leg to the call's conference, heard by the legs their mode names.
    /// </summary>
    /// <param name="conferenceId">The conference.</param>
    /// <param name="supervisorLegId">The supervisor's leg.</param>
    /// <param name="role">The mode's Telnyx role name.</param>
    /// <param name="agentLegId">The monitored agent's leg.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task<bool> JoinSupervisorAsync(
        string conferenceId,
        string supervisorLegId,
        string role,
        string agentLegId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> participants = null;

        if (IsHeardByEverybody(role) || string.IsNullOrWhiteSpace(agentLegId))
        {
            participants = await _apiClient.ListJoinedConferenceParticipantsAsync(conferenceId, cancellationToken);
        }

        var hearers = HearersFor(role, agentLegId, participants, supervisorLegId);
        var joined = await JoinHeardByAsync(conferenceId, supervisorLegId, hearers, $"cc-sv-join-{supervisorLegId}", cancellationToken);

        if (joined.Succeeded || TelnyxApiErrors.IsAlreadyInConference(joined))
        {
            await ReadBackAsync(conferenceId, supervisorLegId, role, "joined", cancellationToken);

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
    /// Changes who hears a joined supervisor, for their new mode.
    /// </summary>
    /// <returns><see langword="true"/> when the supervisor is heard as the mode says; <see langword="false"/> when the call has no running conference or the supervisor is not in it yet.</returns>
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

        var participants = await _apiClient.ReadJoinedConferenceParticipantsAsync(conference.ConferenceId, cancellationToken);

        if (participants is null || !participants.Any(participant => string.Equals(participant.CallControlId, supervisorLegId, StringComparison.Ordinal)))
        {
            // Not answered yet: the role the leg joins with is changed instead.
            return false;
        }

        var hearers = HearersFor(role, agentLegId, participants.Select(participant => participant.CallControlId), supervisorLegId);
        var updated = await _apiClient.UpdateConferenceSupervisorRoleAsync(conference.ConferenceId, supervisorLegId, WhisperRole, hearers, cancellationToken);
        var after = await ReadBackAsync(conference.ConferenceId, supervisorLegId, role, "switched", cancellationToken);

        if (updated.Succeeded && IsHeardBy(after, supervisorLegId, hearers))
        {
            return true;
        }

        // Live, a change of role was answered 200 and changed nothing. Leaving and joining again is a join, which does.
        _logger.LogWarning(
            "Telnyx did not change who hears supervisor leg '{SupervisorLegId}' to {Role} ({StatusCode}); the supervisor rejoins the conference.",
            supervisorLegId.SanitizeLogValue(),
            role.SanitizeLogValue(),
            updated.StatusCode);

        await _apiClient.LeaveConferenceAsync(conference.ConferenceId, supervisorLegId, cancellationToken);

        var rejoined = await JoinHeardByAsync(
            conference.ConferenceId,
            supervisorLegId,
            hearers,
            $"cc-sv-rejoin-{supervisorLegId}-{Guid.NewGuid():N}",
            cancellationToken);

        await ReadBackAsync(conference.ConferenceId, supervisorLegId, role, "rejoined", cancellationToken);

        if (!rejoined.Succeeded && !TelnyxApiErrors.IsAlreadyInConference(rejoined))
        {
            _logger.LogError(
                "Telnyx refused to rejoin supervisor leg '{SupervisorLegId}' as {Role} with status code {StatusCode}. Response: {Response}",
                supervisorLegId.SanitizeLogValue(),
                role.SanitizeLogValue(),
                rejoined.StatusCode,
                rejoined.ErrorBody.SanitizeLogValue());

            return false;
        }

        return true;
    }

    // The one Telnyx role a supervisor is given (see the remarks on the class).
    private const string WhisperRole = "whisper";

    private Task<TelnyxApiResult> JoinHeardByAsync(
        string conferenceId,
        string supervisorLegId,
        IReadOnlyCollection<string> hearers,
        string commandId,
        CancellationToken cancellationToken)
        => _apiClient.JoinConferenceSilentlyAsync(
            conferenceId,
            supervisorLegId,
            WhisperRole,
            hearers,
            commandId: commandId,
            cancellationToken: cancellationToken);

    // Whether Telnyx has the supervisor heard by exactly these legs, and not muted. A list Telnyx did not send is taken on
    // trust: it says nothing either way.
    private static bool IsHeardBy(IReadOnlyList<TelnyxConferenceParticipant> participants, string supervisorLegId, IReadOnlyCollection<string> hearers)
    {
        var supervisor = participants?.FirstOrDefault(participant => string.Equals(participant.CallControlId, supervisorLegId, StringComparison.Ordinal));

        if (supervisor is null || supervisor.Muted)
        {
            return false;
        }

        return supervisor.WhisperCallControlIds is null ||
            supervisor.WhisperCallControlIds.ToHashSet(StringComparer.Ordinal).SetEquals(hearers);
    }

    // Reads the conference back and logs every participant as Telnyx holds it: what the supervisor was actually given, and
    // whether anybody is muted or on hold -- the evidence a live call that sounds wrong needs.
    private async Task<IReadOnlyList<TelnyxConferenceParticipant>> ReadBackAsync(
        string conferenceId,
        string supervisorLegId,
        string role,
        string action,
        CancellationToken cancellationToken)
    {
        var participants = await _apiClient.ReadJoinedConferenceParticipantsAsync(conferenceId, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            var described = participants is null
                ? "(could not be read)"
                : string.Join("; ", participants.Select(participant =>
                    $"{participant.CallControlId} status={participant.Status} muted={participant.Muted} on_hold={participant.OnHold} heard_by=[{(participant.WhisperCallControlIds is null ? "?" : string.Join(",", participant.WhisperCallControlIds))}]"));

            _logger.LogInformation(
                "Supervised conference '{ConferenceId}' after supervisor leg '{SupervisorLegId}' {Action} as {Role}: {Participants}",
                conferenceId.SanitizeLogValue(),
                supervisorLegId.SanitizeLogValue(),
                action,
                role.SanitizeLogValue(),
                described.SanitizeLogValue());
        }

        return participants;
    }

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
