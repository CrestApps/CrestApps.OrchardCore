using CrestApps.Core.Support;
using CrestApps.OrchardCore.Telephony;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// A leg rung to a user's browser that the provider refused as unavailable, rung once more where the phone is now.
/// </summary>
/// <remarks>
/// <para>
/// A phone that reopens, or reconnects after the server restarts, registers on a fresh credential. The one it left
/// behind still reads as registered, because nothing tells the server it is gone, so a call placed in that moment rings
/// the old credential and Telnyx answers SIP 480: the colleague's extension call ended, and a queue call's pre-dialed leg
/// was lost, while the phone sat ready a second later. The refused credential is recorded as unreachable, and the leg is
/// rung once on the user's current credential when there is another one.
/// </para>
/// <para>
/// Only 480 and 404 are read this way. A busy or declining phone (486, 603) is really there and said no, and ringing it
/// again would override the person. A leg that already replaces a refused one is not rung again. The soft phone's own leg
/// of a keypad or extension dial is left alone: the phone named the credential it is registered on, and follows that leg
/// by its identifier.
/// </para>
/// </remarks>
public sealed partial class TelnyxOutboundBridgeOrchestrator
{
    private static bool IsRefusedAsUnavailable(TelnyxCallEvent callEvent)
        => IsHangup(callEvent) && callEvent.SipHangupCause?.Trim() is "480" or "404";

    private bool CanRingAgain(TelnyxCallEvent callEvent, TelnyxOutboundBridgeState state, string userId)
        => _agentEndpointResolver is not null &&
            _options.IsConfigured &&
            state.Redelivered != true &&
            !string.IsNullOrWhiteSpace(userId) &&
            !string.IsNullOrWhiteSpace(callEvent.CallControlId) &&
            callEvent.To?.Trim().StartsWith("sip:", StringComparison.OrdinalIgnoreCase) == true &&
            IsRefusedAsUnavailable(callEvent);

    private async Task<TelnyxAgentEndpointRedelivery> ResolveRingAgainAsync(
        TelnyxCallEvent callEvent,
        string userId,
        string requiredClientCapability,
        string legKind,
        CancellationToken cancellationToken)
    {
        var redelivery = await _agentEndpointResolver.ResolveRedeliveryAsync(userId, callEvent.To.Trim(), requiredClientCapability, cancellationToken);

        if (redelivery is not null)
        {
            _logger.LogWarning(
                "The {LegKind} leg {CallControlId} to credential '{RefusedCredentialId}' of user '{UserId}' was refused as unavailable (SIP {SipHangupCause}); ringing the user's current credential '{CredentialId}' instead.",
                legKind,
                callEvent.CallControlId.SanitizeLogValue(),
                redelivery.UnreachableCredentialId.SanitizeLogValue(),
                userId.SanitizeLogValue(),
                callEvent.SipHangupCause.SanitizeLogValue(),
                redelivery.CredentialId.SanitizeLogValue());
        }

        return redelivery;
    }

    // The colleague's leg of an extension call. The caller's leg is answered and waiting, and carries everything the
    // colleague's leg was dialed with.
    private async Task<bool> TryRingExtensionTargetAgainAsync(TelnyxCallEvent callEvent, TelnyxOutboundBridgeState state, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(state.PeerCallControlId) ||
            state.Detached == true ||
            !string.IsNullOrWhiteSpace(state.TransferOfCallControlId) ||
            !CanRingAgain(callEvent, state, state.VoicemailRecipientUserId))
        {
            return false;
        }

        var caller = await _apiClient.GetCallStatusAsync(state.PeerCallControlId, cancellationToken);

        // The caller hung up, or the caller's leg has already moved on to another leg of this call.
        if (!caller.Succeeded ||
            !caller.IsAlive ||
            !TelnyxOutboundBridgeState.TryParseEncoded(caller.ClientState, out var callerState) ||
            !string.Equals(callerState.PeerCallControlId, callEvent.CallControlId, StringComparison.Ordinal))
        {
            return false;
        }

        var redelivery = await ResolveRingAgainAsync(callEvent, state.VoicemailRecipientUserId, requiredClientCapability: null, "extension", cancellationToken);

        if (redelivery is null)
        {
            return false;
        }

        callerState.Destination = redelivery.Endpoint;

        var destinationLegCallControlId = await DialDestinationAsync(state.PeerCallControlId, callerState, cancellationToken, redelivered: true);

        if (string.IsNullOrWhiteSpace(destinationLegCallControlId))
        {
            return false;
        }

        await RecordPeerAsync(state.PeerCallControlId, callerState, destinationLegCallControlId, cancellationToken);

        return true;
    }

    // The leg an accepted queue call rings to the agent. The caller is answered and waiting for it.
    private async Task<bool> TryRingContactCenterAgentAgainAsync(TelnyxCallEvent callEvent, TelnyxOutboundBridgeState state, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(state.PeerCallControlId) || !CanRingAgain(callEvent, state, state.RingUserId))
        {
            return false;
        }

        var caller = await _apiClient.GetCallStatusAsync(state.PeerCallControlId, cancellationToken);

        if (!caller.Succeeded || !caller.IsAlive)
        {
            return false;
        }

        var redelivery = await ResolveRingAgainAsync(callEvent, state.RingUserId, requiredClientCapability: null, "Contact Center agent", cancellationToken);

        if (redelivery is null)
        {
            return false;
        }

        var originate = new TelnyxOriginateRequest
        {
            ConnectionId = _options.ConnectionId,
            To = redelivery.Endpoint,
            From = _options.DefaultOutboundCallerId,
            ClientState = state.AsRedelivered().ToClientStateJson(),
        };

        // Telnyx de-duplicates by command_id, so a redelivered refusal cannot ring the agent twice.
        originate.AdditionalFields["command_id"] = $"cc-agent-again-{callEvent.CallControlId}";

        try
        {
            var result = await _apiClient.OriginateAsync(originate, cancellationToken);

            if (result.Succeeded && !string.IsNullOrWhiteSpace(result.CallControlId))
            {
                return true;
            }

            _logger.LogWarning(
                "Telnyx refused to ring the agent again for call {PeerCallControlId} ({StatusCode}); the call ends as a failed connect.",
                state.PeerCallControlId.SanitizeLogValue(),
                result.StatusCode);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "An error occurred while ringing the agent again for call {PeerCallControlId}.", state.PeerCallControlId.SanitizeLogValue());
        }

        return false;
    }

    // The leg rung while the offer is still on screen. The coordinator owns which leg the offer is tracking, so it places
    // the new one under the offer's lock.
    private async Task<bool> TryRingPreDialedAgentAgainAsync(TelnyxCallEvent callEvent, TelnyxOutboundBridgeState state, CancellationToken cancellationToken)
    {
        if (_preDialCoordinator is null ||
            string.IsNullOrWhiteSpace(state.ReservationId) ||
            !CanRingAgain(callEvent, state, state.RingUserId))
        {
            return false;
        }

        // Only a phone that said it can hold an offer's leg is rung early, the second time as much as the first.
        var redelivery = await ResolveRingAgainAsync(
            callEvent,
            state.RingUserId,
            TelephonyConstants.SoftPhoneClientCapabilities.HeldOfferLeg,
            "pre-dialed Contact Center agent",
            cancellationToken);

        return redelivery is not null &&
            await _preDialCoordinator.RedialAgentLegAsync(
                TelnyxConstants.ProviderTechnicalName,
                state.ReservationId,
                callEvent.CallControlId,
                redelivery.Endpoint,
                cancellationToken);
    }
}
