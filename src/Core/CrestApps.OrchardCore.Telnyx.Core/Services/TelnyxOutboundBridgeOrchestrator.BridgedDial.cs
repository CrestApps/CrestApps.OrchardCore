using CrestApps.Core.Support;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// A number dialed on the soft phone's keypad and connected on the server: the agent's browser is rung first, the
/// number is dialed once it answers, and the two legs are bridged when the number answers.
/// </summary>
/// <remarks>
/// The soft phone tracks the agent's own leg. Everything the platform does to the call afterwards -- transfer it,
/// merge it, send it digits -- has to reach the other party, whose leg only exists once the agent's browser has
/// answered. So the number's leg is written onto the agent's leg as soon as it is dialed, and read back from there
/// (<see cref="TelnyxTelephonyProvider"/>) when the call is acted on. No server-side registry is needed.
/// </remarks>
public sealed partial class TelnyxOutboundBridgeOrchestrator
{
    private async Task ConnectAgentLegAsync(string agentLegCallControlId, TelnyxOutboundBridgeState state, CancellationToken cancellationToken)
    {
        // A consult with a colleague rings them on a transfer leg, which their phone follows as its own call.
        var destinationLegCallControlId = state.IsConsultAgentLeg && !string.IsNullOrWhiteSpace(state.TargetUserId)
            ? await RingConsultTargetAsync(agentLegCallControlId, state, cancellationToken)
            : await DialDestinationAsync(agentLegCallControlId, state, cancellationToken);

        // An internal extension call is joined through a conference and keeps its own rules, but its agent leg still
        // names the colleague's leg: a merge moves the colleague, not the agent, into the conference it makes.
        if (!string.IsNullOrWhiteSpace(state.VoicemailRecipientUserId))
        {
            if (!string.IsNullOrWhiteSpace(destinationLegCallControlId))
            {
                await RecordPeerAsync(agentLegCallControlId, state, destinationLegCallControlId, cancellationToken);
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(destinationLegCallControlId))
        {
            await ReleaseUnconnectableAgentLegAsync(agentLegCallControlId, cancellationToken);

            return;
        }

        await RecordPeerAsync(agentLegCallControlId, state, destinationLegCallControlId, cancellationToken);
    }

    private async Task RecordPeerAsync(
        string agentLegCallControlId,
        TelnyxOutboundBridgeState state,
        string destinationLegCallControlId,
        CancellationToken cancellationToken)
    {
        var recorded = state.WithPeer(destinationLegCallControlId);

        // Dialed, not answered yet: a merge waits until it is (see MarkPeerAnsweredAsync).
        if (!recorded.IsConsultAgentLeg)
        {
            recorded.PeerAnswered = false;
        }

        var updated = await _apiClient.UpdateClientStateAsync(
            agentLegCallControlId,
            recorded.ToClientStateJson(),
            cancellationToken);

        if (!updated.Succeeded)
        {
            _logger.LogWarning(
                "Telnyx refused to record the other party's leg {DestinationLeg} on the agent leg {AgentLeg} ({StatusCode}); the call works, but it cannot be transferred, merged or sent digits.",
                destinationLegCallControlId.SanitizeLogValue(),
                agentLegCallControlId.SanitizeLogValue(),
                updated.StatusCode);
        }
    }

    // The number could not be dialed, so nothing will ever be joined to the agent's leg, which is answered and silent. A
    // redelivered answer whose dial Telnyx refused as a repeat must not end a call that is up, so the leg is read first:
    // one that already names the number's leg is connected.
    private async Task ReleaseUnconnectableAgentLegAsync(string agentLegCallControlId, CancellationToken cancellationToken)
    {
        var status = await _apiClient.GetCallStatusAsync(agentLegCallControlId, cancellationToken);

        if (status.Succeeded &&
            TelnyxOutboundBridgeState.TryParseEncoded(status.ClientState, out var current) &&
            current.IsBridgedDialAgentLeg)
        {
            return;
        }

        _logger.LogWarning(
            "The number dialed from the soft phone could not be dialed; hanging up the agent leg {AgentLeg}.",
            agentLegCallControlId.SanitizeLogValue());

        await HangupLegAsync(agentLegCallControlId, cancellationToken);
    }

    // The agent hung up. The number's leg goes too -- also while it is still ringing, when there is no bridge yet to
    // take it down -- unless the platform moved it somewhere else first (a transfer or a merge detaches it). So does the
    // colleague of an extension call: its own conference ends with the agent's leg, but a merge moves the colleague
    // into another one, where only this releases them.
    private Task ReleaseRemotePartyAsync(TelnyxOutboundBridgeState state, CancellationToken cancellationToken)
        => (state.IsBridgedDialAgentLeg || state.IsExtensionAgentLeg) && state.Detached != true
            ? HangupLegAsync(state.PeerCallControlId, cancellationToken)
            : Task.CompletedTask;

    // Bridged on the agent's leg with park_after_unbridge=self, as a Contact Center agent leg is: when the number's leg
    // leaves the bridge -- transferred, or moved into a conference -- the agent's leg is parked instead of being hung up,
    // so a merge can still join it to the conference, and a transfer releases it deliberately.
    private Task<bool> BridgeDialedNumberAsync(string agentLegCallControlId, string destinationLegCallControlId, CancellationToken cancellationToken)
        => BridgeAsync(
            callControlId: agentLegCallControlId,
            otherCallControlId: destinationLegCallControlId,
            cancellationToken,
            parkAfterUnbridge: true);
}
