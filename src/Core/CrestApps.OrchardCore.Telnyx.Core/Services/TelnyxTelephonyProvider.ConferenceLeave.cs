using System.Net;
using System.Text.Json;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// The agent leaving a conference while the others stay connected, and the agent ending it for everyone.
/// </summary>
/// <remarks>
/// <para>
/// The soft phone leaves a conference by sending a hang-up flagged <c>conferenceLeave</c> for each of its calls in it.
/// Each of the agent's legs is bridged to, or paired with, a party of its own -- a dialed number's party, an extension
/// call's colleague -- and ending the agent's leg releases that party (see the outbound-bridge orchestrator). So before
/// the agent's leg is hung up, it and its party are both marked detached: the leg's end then says nothing about the party,
/// and the party's own end later says nothing about the leg. The agent's leg joined the conference without
/// <c>end_conference_on_exit</c> (see TelnyxTelephonyProvider.Merge.cs), so the conference runs on without them.
/// </para>
/// <para>
/// A call that is another party's own leg -- a Contact Center caller's -- is never the agent's to hang up when leaving:
/// nothing is sent for it, and it is reported still up.
/// </para>
/// <para>
/// Ending a conference for everyone is Telnyx's own command (<c>POST /conferences/{id}/actions/end</c>), which hangs up
/// every participant still in it; the call the soft phone names is then hung up as any other.
/// </para>
/// </remarks>
public sealed partial class TelnyxTelephonyProvider
{
    private const string ConferenceNameMetadataKey = "conferenceName";

    // Takes the agent's leg for this call out of its conference, leaving its party connected to the others.
    private async Task<TelephonyResult> LeaveConferenceAsync(CallReference call, CancellationToken cancellationToken)
    {
        if (!_options.IsConfigured)
        {
            return NotConfigured();
        }

        if (string.IsNullOrWhiteSpace(call.CallId))
        {
            return TelephonyResult.Failed(S["A call id is required to hang up the call."].Value);
        }

        try
        {
            var status = await _apiClient.GetCallStatusAsync(call.CallId, cancellationToken);

            // A leg Telnyx no longer has, or that has ended, has nobody left to release: it is reported gone.
            if (status.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone || (status.Succeeded && !status.IsAlive))
            {
                return TelephonyResult.Success(BuildCall(call.CallId, CallState.Disconnected, call.Metadata));
            }

            if (!status.Succeeded)
            {
                _logger.LogError(
                    "Telnyx could not read the leg {CallId} the agent is leaving a conference on ({StatusCode}), so it was not hung up.",
                    call.CallId.SanitizeLogValue(),
                    status.StatusCode);

                return TelephonyResult.Failed(S["Telnyx could not take you out of the conference."].Value);
            }

            if (!TelnyxOutboundBridgeState.TryParseEncoded(status.ClientState, out var state) ||
                !(state.IsBridgedDialAgentLeg || state.IsExtensionAgentLeg))
            {
                // Not a leg of the agent's own: it is left in the conference as it is.
                return TelephonyResult.Success(BuildCall(call.CallId, CallState.Connected, call.Metadata));
            }

            var detached = await _apiClient.UpdateClientStateAsync(call.CallId, state.AsDetached().ToClientStateJson(), cancellationToken);

            if (!detached.Succeeded)
            {
                // Hung up still attached, the leg would release its party: the agent leaving must never be what
                // disconnects somebody else.
                _logger.LogError(
                    "Telnyx refused to detach the agent's leg {CallId} from its party ({StatusCode}), so it was not hung up. Response: {Response}",
                    call.CallId.SanitizeLogValue(),
                    detached.StatusCode,
                    detached.ErrorBody.SanitizeLogValue());

                return TelephonyResult.Failed(S["Telnyx could not take you out of the conference."].Value);
            }

            var party = await _apiClient.UpdateClientStateAsync(state.PeerCallControlId, DetachedPartyState(call.CallId).ToClientStateJson(), cancellationToken);

            if (!party.Succeeded)
            {
                _logger.LogWarning(
                    "Telnyx refused to detach the party's leg {PartyCallId} from the agent's leg {CallId} ({StatusCode}); it may already have ended.",
                    state.PeerCallControlId.SanitizeLogValue(),
                    call.CallId.SanitizeLogValue(),
                    party.StatusCode);
            }

            var hungUp = await HangupCallAsync(call, cancellationToken);

            if (hungUp.Succeeded)
            {
                await EndConferenceLeftWithOnePartyAsync(ReadMetadataText(call.Metadata, ConferenceNameMetadataKey), call.CallId, cancellationToken);
            }

            return hungUp;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while taking the agent's leg {CallId} out of its Telnyx conference.", call.CallId.SanitizeLogValue());

            return TelephonyResult.Failed(S["Telnyx could not take you out of the conference."].Value);
        }
    }

    // Ends the conference the call names for everyone in it, then hangs the call up.
    private async Task<TelephonyResult> EndConferenceAsync(CallReference call, CancellationToken cancellationToken)
    {
        var conferenceName = ReadMetadataText(call.Metadata, ConferenceNameMetadataKey);

        if (_options.IsConfigured && !string.IsNullOrWhiteSpace(conferenceName))
        {
            try
            {
                var conference = await _apiClient.FindConferenceByNameAsync(conferenceName, cancellationToken);

                if (conference.Succeeded && !string.IsNullOrWhiteSpace(conference.ConferenceId))
                {
                    var ended = await _apiClient.EndConferenceAsync(conference.ConferenceId, cancellationToken);

                    if (!ended.Succeeded)
                    {
                        _logger.LogWarning(
                            "Telnyx refused to end conference '{ConferenceName}' ({StatusCode}); its calls are hung up one by one.",
                            conferenceName.SanitizeLogValue(),
                            ended.StatusCode);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // The soft phone hangs up every call of the conference too, so a conference that did not end still
                // loses everyone the phone knows of.
                _logger.LogError(ex, "An error occurred while ending Telnyx conference '{ConferenceName}'.", conferenceName.SanitizeLogValue());
            }
        }

        return await HangupCallAsync(call, cancellationToken);
    }

    // With the agent gone, a conference one party is still in has that party talking to nobody: it is ended, which hangs
    // them up, rather than leaving them on a silent line. The soft phone leaves only while two or more parties are in it,
    // but a party can drop in the meantime. The agent's own legs still in it -- the phone's other leave requests in
    // flight -- are not parties. Best effort: a conference that cannot be read is left as it is.
    private async Task EndConferenceLeftWithOnePartyAsync(string conferenceName, string leavingCallId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(conferenceName))
        {
            return;
        }

        try
        {
            var conference = await _apiClient.FindConferenceByNameAsync(conferenceName, cancellationToken);

            if (!conference.Succeeded || string.IsNullOrWhiteSpace(conference.ConferenceId))
            {
                return;
            }

            var participants = await _apiClient.ListConferenceParticipantsAsync(conference.ConferenceId, cancellationToken);
            var parties = 0;

            foreach (var participant in participants)
            {
                if (string.Equals(participant.CallControlId, leavingCallId, StringComparison.Ordinal) ||
                    string.Equals(participant.Status, "left", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var status = await _apiClient.GetCallStatusAsync(participant.CallControlId, cancellationToken);

                if (!status.Succeeded || !status.IsAlive)
                {
                    continue;
                }

                if (TelnyxOutboundBridgeState.TryParseEncoded(status.ClientState, out var state) &&
                    state.Intent == TelnyxOutboundBridgeState.AgentLegIntent)
                {
                    continue;
                }

                parties++;
            }

            // An empty list is a conference that could not be read, or one already over; either way nothing to end.
            if (participants.Count == 0 || parties > 1)
            {
                return;
            }

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "The agent left conference '{ConferenceName}' with {PartyCount} other party in it; ending it rather than leaving them alone.",
                    conferenceName.SanitizeLogValue(),
                    parties);
            }

            await _apiClient.EndConferenceAsync(conference.ConferenceId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The Telnyx conference '{ConferenceName}' could not be read after the agent left it.", conferenceName.SanitizeLogValue());
        }
    }

    // A metadata value as text, whether it arrived as a string or as the JSON the hub deserialized it to.
    private static string ReadMetadataText(IDictionary<string, object> metadata, string key)
    {
        if (metadata is null || !metadata.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            string text => text,
            JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
            JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined } => null,
            _ => value.ToString(),
        };
    }
}
