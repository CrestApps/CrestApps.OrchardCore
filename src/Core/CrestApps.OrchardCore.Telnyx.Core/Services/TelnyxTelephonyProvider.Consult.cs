using CrestApps.Core.Support;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Transferring a number dialed from the soft phone: blind, and warm with a consult first.
/// </summary>
/// <remarks>
/// <para>
/// A blind transfer used to hand the dialed party to the destination with <c>actions/transfer</c>. The colleague it
/// reached got a leg the platform had not placed and knew nothing about -- no client state naming the party, no entry in
/// their history -- so they could not transfer the call again, and a destination that did not answer left the party
/// alone on a parked line. Now the destination is rung on a leg of the platform's own (see
/// <see cref="TelnyxTransferCommands"/>) while the party stays with the agent, held; the party is handed over when that
/// leg answers, and stays with the agent if it does not.
/// </para>
/// <para>
/// A warm transfer rings the agent's own phone for a consult: a second call, on which the agent reaches the destination
/// -- a colleague's browser (joined through a conference, as two browsers need) or a number (bridged, as a keypad dial
/// is) -- while the first call stays held on the phone with its own hold tone. Completing it hands the party to the
/// destination and releases both of the agent's legs; cancelling it hangs up the consult.
/// </para>
/// </remarks>
public sealed partial class TelnyxTelephonyProvider
{
    private async Task<TelephonyResult> RingBlindTransferAsync(TransferRequest request, string destination, BridgedDial bridge, CancellationToken cancellationToken)
    {
        var leg = await _transfers.RingAsync(destination, new TelnyxOutboundBridgeState
        {
            Intent = TelnyxOutboundBridgeState.TransferLegIntent,
            PeerCallControlId = bridge.RemoteLegId,
            TransferOfCallControlId = bridge.AgentLegId,
            TargetUserId = request.IsExtension ? request.TargetUserId : null,
            PartyNumber = bridge.State.Destination,
            CallerDisplayName = ReadRequestMetadata(request, TelephonyConstants.RequestMetadata.SoftPhoneUserDisplayName),
        }, cancellationToken);

        if (leg is null)
        {
            return TelephonyResult.Failed(S["The call could not be transferred. It is still with you."].Value);
        }

        // Until the destination answers, the agent hanging up must not release the party (the transfer still wants
        // them), and the party hanging up must release the leg ringing for them.
        await _transfers.UpdateStateAsync(bridge.AgentLegId, bridge.State.WithPendingTransfer(leg), cancellationToken);
        await _transfers.UpdateStateAsync(bridge.RemoteLegId, new TelnyxOutboundBridgeState
        {
            Intent = TelnyxOutboundBridgeState.DestinationLegIntent,
            PeerCallControlId = bridge.AgentLegId,
            PendingTransferCallControlId = leg,
        }, cancellationToken);

        return TelephonyResult.Success(ConsultCall(bridge.AgentLegId, leg, CallState.OnHold, TelephonyConstants.ConsultStatuses.Ringing, isOnHold: true));
    }

    private async Task<TelephonyResult> StartConsultAsync(TransferRequest request, string destination, BridgedDial bridge, CancellationToken cancellationToken)
    {
        var userId = ReadRequestMetadata(request, TelephonyConstants.RequestMetadata.SoftPhoneUserId);
        var (agentEndpoint, reason) = await ResolveConsultSoftPhoneAsync(
            userId,
            ReadRequestMetadata(request, TelephonyConstants.RequestMetadata.SoftPhoneCredentialId),
            ReadRequestMetadata(request, TelephonyConstants.RequestMetadata.SoftPhoneConnectionId),
            cancellationToken);

        if (agentEndpoint is null)
        {
            _logger.LogWarning(
                "A warm transfer from the soft phone of user '{UserId}' cannot ring the phone for the consult ({Reason}).",
                userId.SanitizeLogValue(),
                reason);

            return TelephonyResult.Failed(S["Your phone cannot be rung for the consult right now. Transfer the call blind instead."].Value);
        }

        var callerId = _options.DefaultOutboundCallerId;
        var originate = new TelnyxOriginateRequest
        {
            ConnectionId = _options.ConnectionId,
            To = agentEndpoint,
            From = callerId,
            ClientState = new TelnyxOutboundBridgeState
            {
                Intent = TelnyxOutboundBridgeState.AgentLegIntent,
                Destination = destination,
                CallerId = callerId,
                CallerDisplayName = ReadRequestMetadata(request, TelephonyConstants.RequestMetadata.SoftPhoneUserDisplayName),
                ConsultOfCallControlId = bridge.AgentLegId,
                PartyCallControlId = bridge.RemoteLegId,
                PartyNumber = bridge.State.Destination,
                TargetUserId = request.IsExtension ? request.TargetUserId : null,
                RingTimeoutSeconds = TelnyxTransferCommands.RingTimeoutSeconds,
            }.ToClientStateJson(),
        };

        var consult = await _apiClient.OriginateAsync(originate, cancellationToken);

        if (!consult.Succeeded || string.IsNullOrWhiteSpace(consult.CallControlId))
        {
            _logger.LogError(
                "Telnyx refused the consult leg of a warm transfer with status code {StatusCode}. Response: {Response}",
                consult.StatusCode,
                consult.ErrorBody.SanitizeLogValue());

            return TelephonyResult.Failed(S["The consult could not be placed. The call is still with you."].Value);
        }

        await _transfers.UpdateStateAsync(bridge.AgentLegId, bridge.State.WithPendingTransfer(consult.CallControlId), cancellationToken);

        var call = ConsultCall(consult.CallControlId, consult.CallControlId, CallState.Connecting, TelephonyConstants.ConsultStatuses.Ringing);
        call.From = callerId;
        call.To = request.To;
        call.StartedUtc = _clock.UtcNow;
        call.Metadata[TelephonyConstants.CallMetadata.ConsultOf] = bridge.AgentLegId;

        if (request.IsExtension)
        {
            call.Metadata[TelephonyConstants.CallMetadata.ExtensionNumber] = request.To;
        }

        return TelephonyResult.Success(call);
    }

    /// <inheritdoc/>
    public async Task<TelephonyResult> GetConsultAsync(ConsultTransferRequest request, CancellationToken cancellationToken = default)
    {
        if (RequireConsult(request) is { } refusal)
        {
            return refusal;
        }

        var (callAlive, _) = await _transfers.ReadAsync(request.CallId, cancellationToken);
        var (legAlive, leg) = await _transfers.ReadAsync(request.ConsultCallId, cancellationToken);

        if (leg is not null && !BelongsTo(leg, request.CallId))
        {
            return ConsultNotFound();
        }

        string status;
        var callEnded = !callAlive;

        if (leg?.IsConsultAgentLeg == true)
        {
            status = !legAlive || !callAlive
                ? TelephonyConstants.ConsultStatuses.Cancelled
                : leg.TargetAnswered == true ? TelephonyConstants.ConsultStatuses.Connected : TelephonyConstants.ConsultStatuses.Ringing;
        }
        else if (leg is not null && leg.Intent != TelnyxOutboundBridgeState.TransferLegIntent)
        {
            // The transfer leg answered and the party was handed to it; its state now describes the call it carries.
            status = TelephonyConstants.ConsultStatuses.Completed;
            callEnded = false;
        }
        else
        {
            status = legAlive && callAlive ? TelephonyConstants.ConsultStatuses.Ringing : TelephonyConstants.ConsultStatuses.Cancelled;
        }

        return TelephonyResult.Success(ConsultCall(request.CallId, request.ConsultCallId, callAlive ? CallState.Connected : CallState.Disconnected, status, callEnded: callEnded));
    }

    /// <inheritdoc/>
    public async Task<TelephonyResult> CompleteConsultAsync(ConsultTransferRequest request, CancellationToken cancellationToken = default)
    {
        if (RequireConsult(request) is { } refusal)
        {
            return refusal;
        }

        var (legAlive, leg) = await _transfers.ReadAsync(request.ConsultCallId, cancellationToken);

        if (leg?.IsConsultAgentLeg != true || !BelongsTo(leg, request.CallId) || !legAlive)
        {
            return ConsultNotFound();
        }

        if (leg.TargetAnswered != true)
        {
            return TelephonyResult.Failed(S["They have not answered yet. Wait for them to answer, or cancel the transfer."].Value);
        }

        if (!await _transfers.CompleteConsultAsync(request.ConsultCallId, leg, hangUpConsultLeg: true, cancellationToken))
        {
            return TelephonyResult.Failed(S["The call could not be handed over. It is still with you."].Value);
        }

        return TelephonyResult.Success(ConsultCall(request.CallId, request.ConsultCallId, CallState.Disconnected, TelephonyConstants.ConsultStatuses.Completed));
    }

    /// <inheritdoc/>
    public async Task<TelephonyResult> CancelConsultAsync(ConsultTransferRequest request, CancellationToken cancellationToken = default)
    {
        if (RequireConsult(request) is { } refusal)
        {
            return refusal;
        }

        var (_, leg) = await _transfers.ReadAsync(request.ConsultCallId, cancellationToken);

        if (leg is null || !BelongsTo(leg, request.CallId))
        {
            return ConsultNotFound();
        }

        if (leg.IsConsultAgentLeg)
        {
            // Hung up as a consult the destination never took, so its end releases the destination and gives the call
            // back to the agent rather than completing the transfer.
            var cancelled = leg.WithPendingTransfer(null);
            cancelled.TargetAnswered = null;

            await _transfers.HangupAsync(request.ConsultCallId, cancelled, cancellationToken);
        }
        else if (leg.Intent == TelnyxOutboundBridgeState.TransferLegIntent)
        {
            // Its end is reported as a transfer nobody answered: the party stays with the agent.
            await _transfers.HangupAsync(request.ConsultCallId, state: null, cancellationToken);
        }
        else
        {
            return TelephonyResult.Failed(S["The call was already handed over."].Value);
        }

        return TelephonyResult.Success(ConsultCall(request.CallId, request.ConsultCallId, CallState.Connected, TelephonyConstants.ConsultStatuses.Cancelled));
    }

    // A consult's agent leg names the call it consults about; a transfer leg, and the leg a party was handed to, the call
    // it was transferred from.
    private static bool BelongsTo(TelnyxOutboundBridgeState leg, string callId)
        => string.Equals(leg.ConsultOfCallControlId, callId, StringComparison.Ordinal) ||
            string.Equals(leg.TransferOfCallControlId, callId, StringComparison.Ordinal);

    private TelephonyResult RequireConsult(ConsultTransferRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.CallId) || string.IsNullOrWhiteSpace(request.ConsultCallId))
        {
            return TelephonyResult.Failed(S["The transfer could not be found."].Value);
        }

        return _options.IsConfigured ? null : NotConfigured();
    }

    private TelephonyResult ConsultNotFound()
        => TelephonyResult.Failed(S["The transfer could not be found."].Value);

    // The call a transfer's result reports, carrying where the transfer stands.
    private static TelephonyCall ConsultCall(
        string callId,
        string consultId,
        CallState state,
        string status,
        bool isOnHold = false,
        bool callEnded = false)
    {
        var call = BuildCall(callId, state, isOnHold: isOnHold);
        var live = status is TelephonyConstants.ConsultStatuses.Ringing or TelephonyConstants.ConsultStatuses.Connected;

        call.Metadata[TelephonyConstants.CallMetadata.ConsultId] = consultId;
        call.Metadata[TelephonyConstants.CallMetadata.ConsultStatus] = status;
        call.Metadata[TelephonyConstants.CallMetadata.ConsultLive] = live;
        call.Metadata[TelephonyConstants.CallMetadata.ConsultCallEnded] = callEnded;

        return call;
    }

    private static string ReadRequestMetadata(TransferRequest request, string key)
        => request.Metadata is not null && request.Metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;
}
