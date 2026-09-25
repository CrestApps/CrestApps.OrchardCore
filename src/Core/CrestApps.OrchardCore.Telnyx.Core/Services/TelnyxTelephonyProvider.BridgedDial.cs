using CrestApps.Core.Support;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// A number dialed on the soft phone's keypad, connected on the server rather than dialed by the browser.
/// </summary>
/// <remarks>
/// <para>
/// A call the browser dials itself goes out on the credential connection, which reports nothing to the platform: there
/// is no <c>call_control_id</c> to transfer, merge or send digits to. So the platform rings the dialing browser's own
/// registered credential from the Call Control connection, dials the number once the browser answers, and bridges the
/// two -- exactly the way an extension call and a Contact Center agent leg are connected.
/// </para>
/// <para>
/// The soft phone tracks the agent's own leg, but Telnyx's commands act on the leg they are given: a transfer of the
/// agent's leg would move the agent, and a conference of it would join the agent to themselves. Every command on such a
/// call is therefore sent to the dialed party's leg, which the bridge orchestrator records on the agent's leg.
/// </para>
/// </remarks>
public sealed partial class TelnyxTelephonyProvider
{
    // Places a keypad dial through the dialing soft phone's own leg, or says it cannot so the phone dials it itself.
    // Nothing is dialed before the answer here is known, so the phone dialing instead can never reach the number twice;
    // an answer Telnyx did not give (a timeout) is not a refusal and is returned as it is.
    private async Task<TelephonyResult> DialThroughSoftPhoneAsync(
        DialRequest request,
        string callerId,
        string credentialId,
        CancellationToken cancellationToken)
    {
        var userId = TryGetMetadataValue(request.Metadata, TelephonyConstants.RequestMetadata.SoftPhoneUserId);
        var (endpoint, reason) = await ResolveDialingSoftPhoneAsync(userId, credentialId, cancellationToken);

        if (endpoint is null)
        {
            _logger.LogWarning(
                "A number dialed from the soft phone of user '{UserId}' cannot be connected through the phone's own leg ({Reason}); the phone dials it from the browser instead, so it cannot be transferred or merged.",
                userId.SanitizeLogValue(),
                reason);

            return BridgeUnavailable();
        }

        var result = await DialBrowserBridgeAsync(request, callerId, endpoint, cancellationToken);

        if (!result.Succeeded && !result.OutcomeUnknown)
        {
            _logger.LogWarning(
                "Telnyx refused the agent leg of a number dialed from the soft phone of user '{UserId}'; no leg was created, so the phone dials it from the browser instead.",
                userId.SanitizeLogValue());

            return BridgeUnavailable();
        }

        return result;
    }

    private async Task<(string Endpoint, string Reason)> ResolveDialingSoftPhoneAsync(string userId, string credentialId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return (null, "the dial names no user");
        }

        // Only the phone that dialed is rung: with the soft phone open in two windows, ringing whichever registered last
        // put the call in the other window, which was not expecting it.
        var live = await _credentialStore.ListLiveByUserAsync(userId.Trim(), _clock.UtcNow, cancellationToken);
        var credential = live.FirstOrDefault(candidate => string.Equals(candidate.CredentialId, credentialId, StringComparison.Ordinal));

        if (credential is null || string.IsNullOrWhiteSpace(credential.SipUsername))
        {
            return (null, "the phone's credential is not live");
        }

        if (!credential.RegisteredUtc.HasValue)
        {
            return (null, "the phone has not registered on its credential");
        }

        // A phone that predates this answers nothing it did not ask for, and would ring its own call as an incoming one.
        if (credential.ClientCapabilities?.Contains(TelephonyConstants.SoftPhoneClientCapabilities.BridgedDialLeg, StringComparer.Ordinal) != true)
        {
            return (null, "the phone does not answer a leg rung back to it");
        }

        var sipDomain = string.IsNullOrWhiteSpace(_options.SipDomain) ? TelnyxConstants.DefaultSipDomain : _options.SipDomain;

        return ($"sip:{credential.SipUsername}@{sipDomain}", null);
    }

    private TelephonyResult BridgeUnavailable()
        => TelephonyResult.Failed(
            S["This call cannot be connected through the soft phone right now."].Value,
            TelephonyConstants.ErrorCodes.BridgeUnavailable);

    /// <summary>
    /// Reads the dialed party's leg off an agent leg that carries one, or <see langword="null"/> for any other call.
    /// </summary>
    private async Task<BridgedDial> FindBridgedDialAsync(string callId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(callId))
        {
            return null;
        }

        var status = await _apiClient.GetCallStatusAsync(callId, cancellationToken);

        return status.Succeeded &&
            TelnyxOutboundBridgeState.TryParseEncoded(status.ClientState, out var state) &&
            state.IsBridgedDialAgentLeg
            ? new BridgedDial(callId, state.PeerCallControlId, state)
            : null;
    }

    // Moves the dialed party to the destination and releases the agent's leg, which the bridge parked when the party
    // left it. The party's leg is marked detached first, so its later end does not reach back for the agent's leg.
    private async Task<TelephonyResult> TransferBridgedDialAsync(TransferRequest request, string destination, BridgedDial bridge, CancellationToken cancellationToken)
    {
        if (request.Mode == TransferMode.Warm)
        {
            return TelephonyResult.Failed(S["A warm transfer is not available for a number dialed from the soft phone. Transfer it blind, or merge the colleague into the call."].Value);
        }

        var transfer = await _apiClient.TransferAsync(
            bridge.RemoteLegId,
            destination,
            _options.DefaultOutboundCallerId,
            RemoteLegState(bridge).ToClientStateJson(),
            cancellationToken);

        if (!transfer.Succeeded)
        {
            _logger.LogError(
                "Telnyx rejected transferring the dialed party of call {CallId} with status code {StatusCode}. Response: {Response}",
                bridge.AgentLegId.SanitizeLogValue(),
                transfer.StatusCode,
                transfer.ErrorBody.SanitizeLogValue());

            return TelephonyResult.Failed(S["Telnyx could not complete the requested operation."].Value);
        }

        await ReleaseAgentLegAsync(bridge, cancellationToken);

        return TelephonyResult.Success(BuildCall(bridge.AgentLegId, CallState.Disconnected));
    }

    private async Task ReleaseAgentLegAsync(BridgedDial bridge, CancellationToken cancellationToken)
    {
        var hangup = await _apiClient.HangupWithStateAsync(bridge.AgentLegId, bridge.State.AsDetached().ToClientStateJson(), cancellationToken);

        if (!hangup.Succeeded && !TelnyxApiErrors.IsCallAlreadyEnded(hangup))
        {
            _logger.LogWarning(
                "The dialed party of call {CallId} was transferred, but the agent's leg could not be hung up ({StatusCode}).",
                bridge.AgentLegId.SanitizeLogValue(),
                hangup.StatusCode);
        }
    }

    // The dialed party's leg as the orchestrator knows it, detached from the agent's leg.
    private static TelnyxOutboundBridgeState RemoteLegState(BridgedDial bridge)
        => new()
        {
            Intent = TelnyxOutboundBridgeState.DestinationLegIntent,
            PeerCallControlId = bridge.AgentLegId,
            Detached = true,
        };

    private sealed record BridgedDial(string AgentLegId, string RemoteLegId, TelnyxOutboundBridgeState State);
}
