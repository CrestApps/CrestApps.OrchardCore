using CrestApps.Core.Support;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Putting the calls of a merge that failed half way back where they were.
/// </summary>
/// <remarks>
/// Live, a merge failed on its last join (the extension had not answered yet) and left a conference with the dialed party
/// and the agent in it, which the phone did not show and every retry then collided with ("Conference with given name
/// already exists"). A merge now records what it moved, and a failure undoes it in reverse:
/// <list type="bullet">
/// <item>a dialed party leaves the conference and is bridged to its agent leg again, as the orchestrator bridged it;</item>
/// <item>an extension call's colleague leaves it and rejoins the extension call's own conference;</item>
/// <item>the agent's leg leaves it, and an extension call's agent leg rejoins its own conference.</item>
/// </list>
/// Everyone leaves with <c>actions/leave</c> rather than the conference being ended: ending it would hang up the party it
/// was made from, and an empty conference expires on its own. A Contact Center caller that joined cannot be bridged back
/// from here (its agent leg is the Contact Center's), so it is left in the conference. Undoing is best effort: each step
/// that Telnyx refuses is logged and the rest go on.
/// </remarks>
public sealed partial class TelnyxTelephonyProvider
{
    private async Task RollBackMergeAsync(List<MergeMove> moves, string conferenceId, CancellationToken cancellationToken)
    {
        if (moves.Count == 0)
        {
            return;
        }

        for (var index = moves.Count - 1; index >= 0; index--)
        {
            var move = moves[index];

            try
            {
                await UndoAsync(move, conferenceId, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not undo the merge of call {CallId} ({Move}).", move.Leg.CallId.SanitizeLogValue(), move.Kind);
            }
        }

        moves.Clear();
    }

    private async Task UndoAsync(MergeMove move, string conferenceId, CancellationToken cancellationToken)
    {
        var leg = move.Leg;

        switch (move.Kind)
        {
            case MergeMoveKind.AgentJoined:
            case MergeMoveKind.AgentLeftExtensionConference:
                if (move.Kind == MergeMoveKind.AgentJoined)
                {
                    await LeaveIfInAsync(conferenceId, leg.CallId, cancellationToken);
                }

                if (!string.IsNullOrWhiteSpace(move.ExtensionConferenceId))
                {
                    await _apiClient.JoinConferenceAsync(move.ExtensionConferenceId, leg.CallId, cancellationToken: cancellationToken);
                }

                return;

            case MergeMoveKind.PrimaryPartyMoved:
            case MergeMoveKind.PartyJoined:
                if (leg.Kind == MergeLegKind.Call)
                {
                    return;
                }

                await LeaveIfInAsync(conferenceId, leg.PartyLegId, cancellationToken);

                if (leg.Kind == MergeLegKind.DialedNumber)
                {
                    // Attached to the agent's leg again, and bridged to it the way the orchestrator bridged them.
                    if (move.Kind == MergeMoveKind.PrimaryPartyMoved)
                    {
                        await _apiClient.UpdateClientStateAsync(leg.PartyLegId, AttachedPartyState(leg).ToClientStateJson(), cancellationToken);
                    }

                    await _apiClient.PostCallActionAsync(
                        leg.CallId,
                        "bridge",
                        new Dictionary<string, object>
                        {
                            ["call_control_id"] = leg.PartyLegId,
                            ["park_after_unbridge"] = "self",
                        },
                        cancellationToken);

                    return;
                }

                if (move.Kind == MergeMoveKind.PrimaryPartyMoved)
                {
                    await _apiClient.UpdateClientStateAsync(leg.PartyLegId, AttachedPartyState(leg).ToClientStateJson(), cancellationToken);
                }

                var extension = await _apiClient.FindLiveConferenceByNameAsync(ExtensionConferenceName(leg.CallId), cancellationToken);

                if (extension.Succeeded && !string.IsNullOrWhiteSpace(extension.ConferenceId))
                {
                    await _apiClient.JoinConferenceAsync(extension.ConferenceId, leg.PartyLegId, cancellationToken: cancellationToken);
                }

                return;
        }
    }

    private async Task LeaveIfInAsync(string conferenceId, string callControlId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(conferenceId) || string.IsNullOrWhiteSpace(callControlId))
        {
            return;
        }

        var left = await _apiClient.LeaveConferenceAsync(conferenceId, callControlId, cancellationToken);

        if (!left.Succeeded && _logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Telnyx did not take {CallId} out of conference {ConferenceId} ({StatusCode}) while undoing a merge.", callControlId.SanitizeLogValue(), conferenceId.SanitizeLogValue(), left.StatusCode);
        }
    }

    // The party's state as the orchestrator set it before the merge detached it.
    private static TelnyxOutboundBridgeState AttachedPartyState(MergeLeg leg)
        => new()
        {
            Intent = TelnyxOutboundBridgeState.DestinationLegIntent,
            PeerCallControlId = leg.CallId,
            VoicemailRecipientUserId = leg.Kind == MergeLegKind.Extension ? leg.State?.VoicemailRecipientUserId : null,
        };

    private enum MergeMoveKind
    {
        // The primary call's party is in the conference (it was made from it).
        PrimaryPartyMoved,

        // The agent's leg left its extension conference but did not make it into the merge's.
        AgentLeftExtensionConference,

        // The agent's leg joined the conference.
        AgentJoined,

        // Another call's party joined the conference.
        PartyJoined,
    }

    private sealed record MergeMove(MergeMoveKind Kind, MergeLeg Leg, string ExtensionConferenceId);
}
