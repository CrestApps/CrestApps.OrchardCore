using CrestApps.Core.Support;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telnyx.Models;
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
        var reason = WhyCannotRingBack(credential);

        return reason is null ? (SoftPhoneEndpoint(credential), null) : (null, reason);
    }

    // The phone a warm transfer's consult rings: the agent's own, which asked for it. The phone names the credential it is
    // registered on, but a page opened before phones named it names none, and one whose credential was renewed may name
    // the one it replaced -- and refusing the consult then left an agent whose phone was ready unable to transfer warm.
    // So an unusable name falls back to the caller's own credential registered from the connection that asked, else to
    // their most recently registered one. Only the caller's own credentials are ever considered.
    private async Task<(string Endpoint, string Reason)> ResolveConsultSoftPhoneAsync(
        string userId,
        string credentialId,
        string connectionId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return (null, "the transfer names no user");
        }

        var live = await _credentialStore.ListLiveByUserAsync(userId.Trim(), _clock.UtcNow, cancellationToken);
        var named = string.IsNullOrWhiteSpace(credentialId)
            ? null
            : live.FirstOrDefault(candidate => string.Equals(candidate.CredentialId, credentialId, StringComparison.Ordinal));

        if (named is not null && WhyCannotRingBack(named) is null)
        {
            return (SoftPhoneEndpoint(named), null);
        }

        var ringable = TelnyxAgentCredentialSelection.OrderByDeliveryPreference(live.Where(candidate => WhyCannotRingBack(candidate) is null));
        var fallback = ringable.FirstOrDefault(candidate =>
                !string.IsNullOrWhiteSpace(connectionId) &&
                string.Equals(candidate.RegisteredConnectionId, connectionId, StringComparison.Ordinal)) ??
            (ringable.Count > 0 ? ringable[0] : null);

        if (fallback is null)
        {
            return (null, named is not null ? WhyCannotRingBack(named) : "no phone of the user can be rung back for the consult");
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "A warm transfer from the soft phone of user '{UserId}' named no credential that can be rung back; the consult rings the user's registered credential '{CredentialId}' instead.",
                userId.SanitizeLogValue(),
                fallback.CredentialId.SanitizeLogValue());
        }

        return (SoftPhoneEndpoint(fallback), null);
    }

    // Why a credential cannot be rung back for a leg its phone answers itself, or null when it can.
    private static string WhyCannotRingBack(TelnyxAgentCredential credential)
    {
        if (credential is null || string.IsNullOrWhiteSpace(credential.SipUsername))
        {
            return "the phone's credential is not live";
        }

        if (!credential.RegisteredUtc.HasValue)
        {
            return "the phone has not registered on its credential";
        }

        // A phone that predates this answers nothing it did not ask for, and would ring its own call as an incoming one.
        return credential.ClientCapabilities?.Contains(TelephonyConstants.SoftPhoneClientCapabilities.BridgedDialLeg, StringComparer.Ordinal) == true
            ? null
            : "the phone does not answer a leg rung back to it";
    }

    private string SoftPhoneEndpoint(TelnyxAgentCredential credential)
    {
        var sipDomain = string.IsNullOrWhiteSpace(_options.SipDomain) ? TelnyxConstants.DefaultSipDomain : _options.SipDomain;

        return $"sip:{credential.SipUsername}@{sipDomain}";
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

    // A blind transfer rings its destination and hands the party over once it answers; a warm one rings the agent's own
    // phone for a consult first (see TelnyxTelephonyProvider.Consult.cs).
    private Task<TelephonyResult> TransferBridgedDialAsync(TransferRequest request, string destination, BridgedDial bridge, CancellationToken cancellationToken)
        => request.Mode == TransferMode.Warm
            ? StartConsultAsync(request, destination, bridge, cancellationToken)
            : RingBlindTransferAsync(request, destination, bridge, cancellationToken);

    private sealed record BridgedDial(string AgentLegId, string RemoteLegId, TelnyxOutboundBridgeState State);
}
