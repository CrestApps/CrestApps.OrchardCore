using CrestApps.Core.Support;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Merging calls into a Telnyx conference, and sending digits, on the leg that reaches the other party.
/// </summary>
/// <remarks>
/// <para>
/// For a number dialed from the soft phone the call the phone names is the agent's own leg, bridged to the dialed
/// party's leg with <c>park_after_unbridge=self</c>. Conferencing the agent's legs would join the agent to themselves
/// once per call. So the conference is made from the first call's dialed party (which parks the agent's first leg), the
/// agent's first leg joins it -- without <c>end_conference_on_exit</c>, so the agent leaving leaves the others connected
/// (see TelnyxTelephonyProvider.ConferenceLeave.cs) -- and every other call's dialed party joins it. The agent's other
/// legs stay parked, one per participant: hanging one up ends that participant (the orchestrator releases a leg's dialed
/// party with it), and a participant hanging up releases its leg.
/// </para>
/// <para>
/// An internal extension call is already a conference, <c>ext-{agent leg}</c>, made from the agent's leg with the
/// colleague as an ordinary participant (see TelnyxOutboundBridgeOrchestrator.ExtensionConference.cs). A call that
/// created a conference is hung up when that conference is ended, wherever it has gone since, so a merge never lets a
/// party's call become bound to a conference the agent's leaving could end. A dialed number leads a merge whenever there
/// is one, and the colleague joins its conference while the agent's extension leg stays behind in its own, the way a
/// dialed number's agent leg stays parked. With no dialed number, a new conference is made from the first colleague, and
/// the agent's extension leg leaves its own conference (<c>actions/leave</c>) and joins the new one without
/// <c>end_conference_on_exit</c>: the old one, empty, expires, and a call that left the conference it created that way
/// outlives it.
/// </para>
/// <para>
/// Every call is read before anything moves, so a call that cannot be merged -- one whose party has not answered yet --
/// refuses the merge with nothing changed. A merge that fails once calls have moved puts them back where they were (see
/// TelnyxTelephonyProvider.MergeRollback.cs), and a retry that finds the conference of its name already made uses it.
/// </para>
/// </remarks>
public sealed partial class TelnyxTelephonyProvider
{
    /// <inheritdoc/>
    public async Task<TelephonyResult> MergeAsync(MergeRequest request, CancellationToken cancellationToken = default)
    {
        var callIds = request?.GetCallIds();

        if (callIds is null || callIds.Count < 2)
        {
            return TelephonyResult.Failed(S["At least two calls are required to merge calls."].Value);
        }

        if (!_options.IsConfigured)
        {
            return NotConfigured();
        }

        var moves = new List<MergeMove>();
        string conferenceId = null;

        try
        {
            var legs = new List<MergeLeg>(callIds.Count);

            foreach (var callId in callIds)
            {
                var leg = await FindMergeLegAsync(callId, cancellationToken);

                if (leg is null || leg.State?.PeerAnswered == false)
                {
                    // Telnyx refuses to join a call nobody has answered ("Call not answered yet"): refused here, before
                    // anything moves.
                    return TelephonyResult.Failed(
                        S["A call you are merging has not been answered yet. Merge the calls once they pick up."].Value,
                        TelephonyConstants.ErrorCodes.NotAnswered);
                }

                legs.Add(leg);
            }

            // A new conference is led by a call that carries the agent into it -- a dialed number's agent leg, else an
            // extension call's -- rather than by another call's own leg: a conference made from a Contact Center
            // caller's leg would take it off the agent's leg it is bridged to, and leave the agent outside.
            if (string.IsNullOrWhiteSpace(request.ConferenceName))
            {
                legs =
                [
                    .. legs.Where(leg => leg.Kind == MergeLegKind.DialedNumber),
                    .. legs.Where(leg => leg.Kind == MergeLegKind.Extension),
                    .. legs.Where(leg => leg.Kind == MergeLegKind.Call),
                ];
            }

            var primary = legs[0];
            var conferenceName = string.IsNullOrWhiteSpace(request.ConferenceName)
                ? $"conf-{primary.CallId}"
                : request.ConferenceName;

            // A merge that names its conference is adding calls to one already running, which the primary call is in:
            // the others join it. Creating another from the primary would take it out of the first.
            conferenceId = string.IsNullOrWhiteSpace(request.ConferenceName)
                ? null
                : (await _apiClient.FindLiveConferenceByNameAsync(conferenceName, cancellationToken)).ConferenceId;

            if (string.IsNullOrWhiteSpace(conferenceId))
            {
                (conferenceId, conferenceName) = await CreateConferenceFromAsync(conferenceName, primary, moves, cancellationToken);

                if (conferenceId is null)
                {
                    await RollBackMergeAsync(moves, conferenceId, cancellationToken);

                    return TelephonyResult.Failed(S["Telnyx could not merge the calls."].Value);
                }
            }

            foreach (var secondary in legs.Skip(1))
            {
                // A dialed number joins as its dialed party and an extension call as its colleague; the agent's own leg
                // for it stays where it is.
                var joinResult = await _apiClient.JoinConferenceAsync(conferenceId, secondary.PartyLegId, cancellationToken: cancellationToken);

                // Merging calls already in the conference again (a second press of Merge, or a retry) leaves them there.
                if (!joinResult.Succeeded && TelnyxApiErrors.IsAlreadyInConference(joinResult))
                {
                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug("Telnyx leg {CallId} is already in conference {ConferenceId}; nothing to join.", secondary.PartyLegId.SanitizeLogValue(), conferenceId.SanitizeLogValue());
                    }

                    continue;
                }

                if (!joinResult.Succeeded)
                {
                    _logger.LogError(
                        "Telnyx rejected a conference join request with status code {StatusCode}; the merge is rolled back. Response: {Response}",
                        joinResult.StatusCode,
                        joinResult.ErrorBody.SanitizeLogValue());

                    await RollBackMergeAsync(moves, conferenceId, cancellationToken);

                    return TelephonyResult.Failed(S["Telnyx could not merge the calls."].Value);
                }

                moves.Add(new MergeMove(MergeMoveKind.PartyJoined, secondary, null));
            }

            return TelephonyResult.Success(BuildCall(
                primary.CallId,
                CallState.Connected,
                new Dictionary<string, object>
                {
                    ["isConference"] = true,
                    ["conferenceId"] = conferenceId,
                    // The soft phone names this conference when it adds a call to it.
                    ["conferenceName"] = conferenceName,
                    ["participantCount"] = callIds.Count,
                }));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while merging Telnyx calls.");

            await RollBackMergeAsync(moves, conferenceId, CancellationToken.None);

            return TelephonyResult.Failed(S["Telnyx could not merge the calls."].Value);
        }
    }

    // Makes the conference the primary call leads, and moves the agent into it, recording each move for a rollback; or,
    // for an extension call whose colleague Telnyx will not move, adopts the extension call's own conference. Returns it
    // with its name, or (null, name) when Telnyx refused. A conference of that name left by an earlier attempt is used.
    private async Task<(string ConferenceId, string ConferenceName)> CreateConferenceFromAsync(
        string conferenceName,
        MergeLeg primary,
        List<MergeMove> moves,
        CancellationToken cancellationToken)
    {
        // A dialed number's party, or an extension call's colleague, moves into the new conference detached from the
        // agent's leg, so its leaving does not reach back for the agent; any other call joins it as it is.
        var created = primary.Kind == MergeLegKind.Call
            ? await _apiClient.CreateConferenceAsync(conferenceName, primary.CallId, cancellationToken: cancellationToken)
            : await _apiClient.CreateConferenceWithStateAsync(conferenceName, primary.PartyLegId, DetachedPartyState(primary.CallId).ToClientStateJson(), cancellationToken);
        var conferenceId = created.Succeeded ? created.ConferenceId : null;

        if (string.IsNullOrWhiteSpace(conferenceId) && TelnyxApiErrors.IsConferenceNameTaken(created))
        {
            // Made by an attempt that did not finish: the party joins it (or is in it already).
            conferenceId = await RejoinExistingConferenceAsync(conferenceName, primary, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(conferenceId))
        {
            if (primary.Kind == MergeLegKind.Extension)
            {
                _logger.LogWarning(
                    "Telnyx refused to move the colleague's leg {CallId} into a new conference ({StatusCode}); the extension call's own conference is used.",
                    primary.PartyLegId.SanitizeLogValue(),
                    created.StatusCode);

                return (await AdoptExtensionConferenceAsync(primary, cancellationToken), ExtensionConferenceName(primary.CallId));
            }

            _logger.LogError(
                "Telnyx rejected a conference creation request with status code {StatusCode}. Response: {Response}",
                created.StatusCode,
                created.ErrorBody.SanitizeLogValue());

            return (null, conferenceName);
        }

        moves.Add(new MergeMove(MergeMoveKind.PrimaryPartyMoved, primary, null));

        if (primary.Kind == MergeLegKind.Call)
        {
            return (conferenceId, conferenceName);
        }

        // An extension call's agent leg leaves its own conference first, rather than being moved by the join: that
        // conference, now empty, expires, and a call that left the conference it created outlives it ending.
        var extensionConferenceId = primary.Kind == MergeLegKind.Extension
            ? await LeaveExtensionConferenceAsync(primary.CallId, cancellationToken)
            : null;

        // The agent joins the conference on the leg its party left: the leg the bridge parked, or an extension call's
        // leg. Without end_conference_on_exit, the agent leaving leaves the others connected.
        var joined = await _apiClient.JoinConferenceWithStateAsync(
            conferenceId,
            primary.CallId,
            endConferenceOnExit: false,
            clientStateJson: null,
            cancellationToken);

        if (!joined.Succeeded && !TelnyxApiErrors.IsAlreadyInConference(joined))
        {
            _logger.LogError(
                "Telnyx rejected joining the agent's leg {CallId} to conference '{ConferenceName}' with status code {StatusCode}. Response: {Response}",
                primary.CallId.SanitizeLogValue(),
                conferenceName.SanitizeLogValue(),
                joined.StatusCode,
                joined.ErrorBody.SanitizeLogValue());

            moves.Add(new MergeMove(MergeMoveKind.AgentLeftExtensionConference, primary, extensionConferenceId));

            return (null, conferenceName);
        }

        moves.Add(new MergeMove(MergeMoveKind.AgentJoined, primary, extensionConferenceId));

        return (conferenceId, conferenceName);
    }

    // The conference an earlier, unfinished attempt made under this name: the primary's party joins it again (a party
    // already in it stays), detached as a new conference would have made it.
    private async Task<string> RejoinExistingConferenceAsync(string conferenceName, MergeLeg primary, CancellationToken cancellationToken)
    {
        var existing = await _apiClient.FindLiveConferenceByNameAsync(conferenceName, cancellationToken);

        if (!existing.Succeeded || string.IsNullOrWhiteSpace(existing.ConferenceId))
        {
            return null;
        }

        var partyLegId = primary.Kind == MergeLegKind.Call ? primary.CallId : primary.PartyLegId;
        var joined = await _apiClient.JoinConferenceAsync(existing.ConferenceId, partyLegId, cancellationToken: cancellationToken);

        if (!joined.Succeeded && !TelnyxApiErrors.IsAlreadyInConference(joined))
        {
            return null;
        }

        if (primary.Kind != MergeLegKind.Call)
        {
            await _apiClient.UpdateClientStateAsync(partyLegId, DetachedPartyState(primary.CallId).ToClientStateJson(), cancellationToken);
        }

        return existing.ConferenceId;
    }

    // Takes the agent's extension leg out of its call's own conference, and returns that conference's id, or null when it
    // is not running.
    private async Task<string> LeaveExtensionConferenceAsync(string agentLegCallControlId, CancellationToken cancellationToken)
    {
        var conference = await _apiClient.FindLiveConferenceByNameAsync(ExtensionConferenceName(agentLegCallControlId), cancellationToken);

        if (!conference.Succeeded || string.IsNullOrWhiteSpace(conference.ConferenceId))
        {
            return null;
        }

        var left = await _apiClient.LeaveConferenceAsync(conference.ConferenceId, agentLegCallControlId, cancellationToken);

        if (!left.Succeeded)
        {
            _logger.LogWarning(
                "Telnyx did not take the agent's extension leg {CallId} out of its conference ({StatusCode}); it is moved by the join instead.",
                agentLegCallControlId.SanitizeLogValue(),
                left.StatusCode);
        }

        return conference.ConferenceId;
    }

    // Whether the soft phone flagged this hang-up with a request metadata key (see TelephonyConstants.RequestMetadata).
    private static bool HasRequestFlag(CallReference call, string key)
        => call?.Metadata is not null &&
            call.Metadata.TryGetValue(key, out var value) &&
            string.Equals(value?.ToString(), "true", StringComparison.OrdinalIgnoreCase);

    // Hangs up one participant of a conference. The call the conference was made from is also the agent's own way into
    // it, and its party was detached from it by the merge: hanging up the call would take the agent out of the
    // conference, so only the party's leg is hung up and the call is reported still up, its participant gone. Any other
    // call is hung up as it is -- a parked agent leg releases its party with it.
    private async Task<TelephonyResult> HangupConferenceParticipantAsync(CallReference call, CancellationToken cancellationToken)
    {
        if (_options.IsConfigured && !string.IsNullOrWhiteSpace(call.CallId))
        {
            try
            {
                var leg = await FindMergeLegAsync(call.CallId, cancellationToken);

                if (leg is not null &&
                    !string.IsNullOrWhiteSpace(leg.PartyLegId) &&
                    !string.Equals(leg.PartyLegId, leg.CallId, StringComparison.Ordinal))
                {
                    var party = await _apiClient.GetCallStatusAsync(leg.PartyLegId, cancellationToken);

                    if (party.Succeeded &&
                        TelnyxOutboundBridgeState.TryParseEncoded(party.ClientState, out var partyState) &&
                        partyState.Detached == true)
                    {
                        var metadata = call.Metadata is null
                            ? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                            : new Dictionary<string, object>(call.Metadata, StringComparer.OrdinalIgnoreCase);

                        metadata[TelephonyConstants.CallMetadata.ParticipantLeft] = true;

                        return await ExecuteActionAsync(
                            leg.PartyLegId,
                            "hangup",
                            body: null,
                            () => BuildCall(call.CallId, CallState.Connected, metadata),
                            cancellationToken,
                            succeedWhenMissing: true,
                            succeedWhenEnded: true);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while reading the Telnyx conference participant {CallId} to hang up.", call.CallId.SanitizeLogValue());

                return TelephonyResult.Failed(S["Telnyx could not hang up the participant."].Value);
            }
        }

        return await HangupCallAsync(call, cancellationToken);
    }

    // How a call takes part in a merge, or null for an extension call whose colleague's leg is not known yet.
    private async Task<MergeLeg> FindMergeLegAsync(string callId, CancellationToken cancellationToken)
    {
        var status = await _apiClient.GetCallStatusAsync(callId, cancellationToken);

        if (!status.Succeeded || !TelnyxOutboundBridgeState.TryParseEncoded(status.ClientState, out var state))
        {
            return new MergeLeg(callId, callId, MergeLegKind.Call, null);
        }

        if (state.IsBridgedDialAgentLeg)
        {
            return new MergeLeg(callId, state.PeerCallControlId, MergeLegKind.DialedNumber, state);
        }

        if (state.Intent == TelnyxOutboundBridgeState.AgentLegIntent && !string.IsNullOrWhiteSpace(state.VoicemailRecipientUserId))
        {
            return state.IsExtensionAgentLeg
                ? new MergeLeg(callId, state.PeerCallControlId, MergeLegKind.Extension, state)
                : null;
        }

        return new MergeLeg(callId, callId, MergeLegKind.Call, state);
    }

    // The extension call's own conference becomes the merge's. The agent's leg stays in it as it is, and the colleague is
    // detached, so their hanging up leaves the others on.
    private async Task<string> AdoptExtensionConferenceAsync(MergeLeg primary, CancellationToken cancellationToken)
    {
        var name = ExtensionConferenceName(primary.CallId);
        var conference = await _apiClient.FindConferenceByNameAsync(name, cancellationToken);

        if (!conference.Succeeded || string.IsNullOrWhiteSpace(conference.ConferenceId))
        {
            _logger.LogWarning(
                "The extension call {CallId} could not be merged: its conference '{ConferenceName}' is not running.",
                primary.CallId.SanitizeLogValue(),
                name.SanitizeLogValue());

            return null;
        }

        var detached = await _apiClient.UpdateClientStateAsync(primary.PartyLegId, DetachedPartyState(primary.CallId).ToClientStateJson(), cancellationToken);

        if (!detached.Succeeded)
        {
            _logger.LogWarning(
                "Telnyx refused to detach the colleague's leg {CallId} of a merged extension call ({StatusCode}); their hanging up will end the conference.",
                primary.PartyLegId.SanitizeLogValue(),
                detached.StatusCode);
        }

        return conference.ConferenceId;
    }


    // Named after the caller's leg by the orchestrator when it connects an internal extension call.
    internal static string ExtensionConferenceName(string agentLegCallControlId)
        => $"ext-{agentLegCallControlId}";

    // A party that has left the agent's leg it was connected to, so its end no longer reaches back for that leg.
    private static TelnyxOutboundBridgeState DetachedPartyState(string agentLegCallControlId)
        => new()
        {
            Intent = TelnyxOutboundBridgeState.DestinationLegIntent,
            PeerCallControlId = agentLegCallControlId,
            Detached = true,
        };

    // Digits are played from a leg to the party at its other end, so for a dialed number they are sent from the dialed
    // party's leg: sent from the agent's leg they would be played to the agent's own browser.
    private async Task<TelephonyResult> SendDigitsCoreAsync(SendDigitsRequest request, CancellationToken cancellationToken)
    {
        var bridge = _options.IsConfigured ? await FindBridgedDialAsync(request.CallId, cancellationToken) : null;

        return await ExecuteActionAsync(
            bridge?.RemoteLegId ?? request.CallId,
            "send_dtmf",
            new Dictionary<string, object> { ["digits"] = request.Digits },
            () => null,
            cancellationToken);
    }

    private enum MergeLegKind
    {
        // Any other call: it joins the conference itself.
        Call,

        // A number dialed from the soft phone: its dialed party joins, its agent leg stays parked.
        DialedNumber,

        // An internal extension call: its colleague joins, or its conference is the merge's.
        Extension,
    }

    private sealed record MergeLeg(string CallId, string PartyLegId, MergeLegKind Kind, TelnyxOutboundBridgeState State);
}
