using CrestApps.Core.Support;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// The webhook side of transferring a number dialed from the soft phone (see <see cref="TelnyxTransferCommands"/>).
/// </summary>
/// <remarks>
/// <para>
/// A blind transfer's leg answering hands the party over; the leg ending unanswered -- declined, busy, rung out, or
/// cancelled -- leaves the party with the agent, still held, or, when the agent has gone meanwhile, sends a colleague's
/// caller to that colleague's voicemail (and releases a party whose outside destination did not answer).
/// </para>
/// <para>
/// A consult's destination answering joins the agent's consult leg (a colleague, through a conference) and marks the
/// consult answered; its hanging up ends the consult and gives the call back to the agent, held. The agent's consult
/// leg ending on its own -- the agent hung up, or closed the phone -- completes the transfer if the destination had
/// answered, the way most phone systems treat hanging up during a consult; otherwise it releases the destination.
/// </para>
/// </remarks>
public sealed partial class TelnyxOutboundBridgeOrchestrator
{
    private async Task<TelnyxOutboundBridgeLeg> AdvanceTransferLegAsync(
        TelnyxCallEvent callEvent,
        TelnyxOutboundBridgeState state,
        bool isAnswered,
        CancellationToken cancellationToken)
    {
        // A colleague's leg is a call in their history: its answer and its end are theirs to see. Nothing before the
        // answer is shown -- their phone is ringing it already, and a "dialing" report would read as a call in progress.
        var colleagueLeg = !string.IsNullOrWhiteSpace(state.TargetUserId);
        var shown = colleagueLeg ? TelnyxOutboundBridgeLeg.None : TelnyxOutboundBridgeLeg.DestinationLeg;
        var isHangup = IsHangup(callEvent);

        if ((!isAnswered && !isHangup) || !_options.IsConfigured || string.IsNullOrWhiteSpace(callEvent.CallControlId))
        {
            return TelnyxOutboundBridgeLeg.DestinationLeg;
        }

        var isConsult = !string.IsNullOrWhiteSpace(state.ConferenceName);

        if (isConsult && isAnswered)
        {
            await JoinConsultAsync(callEvent.CallControlId, state, cancellationToken);
        }
        else if (isConsult)
        {
            await ConsultTargetLeftAsync(state.PeerCallControlId, state.TransferOfCallControlId, cancellationToken);
        }
        else if (isAnswered)
        {
            await HandOverOrReturnAsync(callEvent.CallControlId, state, colleagueLeg, cancellationToken);
        }
        else
        {
            await TransferUnansweredAsync(callEvent, state, cancellationToken);
        }

        return shown;
    }

    private async Task HandOverOrReturnAsync(string transferLegId, TelnyxOutboundBridgeState state, bool colleagueLeg, CancellationToken cancellationToken)
    {
        if (await _transfers.HandOverAsync(transferLegId, colleagueLeg, state.PeerCallControlId, state.TransferOfCallControlId, cancellationToken))
        {
            return;
        }

        // The party could not be moved -- most likely they hung up as the destination answered. The destination is not
        // left on a silent line; the party, if still there, stays with the agent.
        await _transfers.HangupAsync(transferLegId, state: null, cancellationToken);
    }

    private async Task TransferUnansweredAsync(TelnyxCallEvent callEvent, TelnyxOutboundBridgeState state, CancellationToken cancellationToken)
    {
        var (agentAlive, agentState) = await _transfers.ReadAsync(state.TransferOfCallControlId, cancellationToken);

        if (agentAlive)
        {
            // The party never left the agent. The agent hanging up releases them again from now on.
            if (agentState is not null &&
                string.Equals(agentState.PendingTransferCallControlId, callEvent.CallControlId, StringComparison.Ordinal))
            {
                await _transfers.UpdateStateAsync(state.TransferOfCallControlId, agentState.WithPendingTransfer(null), cancellationToken);
            }

            await _transfers.UpdateStateAsync(state.PeerCallControlId, new TelnyxOutboundBridgeState
            {
                Intent = TelnyxOutboundBridgeState.DestinationLegIntent,
                PeerCallControlId = state.TransferOfCallControlId,
            }, cancellationToken);

            return;
        }

        // The agent left while the transfer rang, and nobody took the call: the party is alone on a parked line.
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "A transfer nobody answered (cause {HangupCause}) left party {PartyLeg} without its agent; {Outcome}.",
                callEvent.HangupCause.SanitizeLogValue(),
                state.PeerCallControlId.SanitizeLogValue(),
                string.IsNullOrWhiteSpace(state.TargetUserId) ? "releasing it" : "sending it to the colleague's voicemail");
        }

        if (string.IsNullOrWhiteSpace(state.TargetUserId))
        {
            await HangupLegAsync(state.PeerCallControlId, cancellationToken);

            return;
        }

        await RouteToVoicemailAsync(state.PeerCallControlId, state.TargetUserId, cancellationToken);
    }

    // A consult's colleague answered: they and the agent's consult leg meet in a conference (two browser legs pass audio
    // both ways only through the mixer). Nobody's leaving ends it for the other, so the agent hanging up can still hand
    // the colleague the call.
    private async Task JoinConsultAsync(string transferLegId, TelnyxOutboundBridgeState state, CancellationToken cancellationToken)
    {
        var conference = await _apiClient.CreateConferenceAsync(
            state.ConferenceName,
            transferLegId,
            commandId: $"consult-conf-{transferLegId}",
            cancellationToken);

        if (!conference.Succeeded || string.IsNullOrWhiteSpace(conference.ConferenceId))
        {
            _logger.LogError(
                "Telnyx rejected the consult conference '{ConferenceName}' with status code {StatusCode}. Response: {Response}",
                state.ConferenceName.SanitizeLogValue(),
                conference.StatusCode,
                conference.ErrorBody.SanitizeLogValue());

            return;
        }

        var join = await _apiClient.JoinConferenceAsync(
            conference.ConferenceId,
            state.PeerCallControlId,
            endConferenceOnExit: false,
            commandId: $"consult-join-{state.PeerCallControlId}",
            cancellationToken);

        if (!join.Succeeded)
        {
            _logger.LogError(
                "Telnyx rejected joining the agent's consult leg to '{ConferenceName}' with status code {StatusCode}. Response: {Response}",
                state.ConferenceName.SanitizeLogValue(),
                join.StatusCode,
                join.ErrorBody.SanitizeLogValue());

            return;
        }

        await MarkConsultAnsweredAsync(state.PeerCallControlId, cancellationToken);
    }

    private async Task MarkConsultAnsweredAsync(string consultLegId, CancellationToken cancellationToken)
    {
        var (alive, consult) = await _transfers.ReadAsync(consultLegId, cancellationToken);

        if (!alive || consult?.IsConsultAgentLeg != true)
        {
            return;
        }

        var answered = consult.WithPendingTransfer(consult.PendingTransferCallControlId);
        answered.TargetAnswered = true;

        await _transfers.UpdateStateAsync(consultLegId, answered, cancellationToken);
    }

    // The destination of a consult hung up. The consult leg goes -- reported detached, so its end neither completes the
    // transfer nor looks for a destination -- and the call being consulted about is the agent's again, still held.
    private async Task ConsultTargetLeftAsync(string consultLegId, string agentLegId, CancellationToken cancellationToken)
    {
        await ReturnCallToAgentAsync(agentLegId, consultLegId, cancellationToken);

        await _transfers.HangupAsync(consultLegId, new TelnyxOutboundBridgeState
        {
            Intent = TelnyxOutboundBridgeState.AgentLegIntent,
            ConsultOfCallControlId = agentLegId,
            Detached = true,
        }, cancellationToken);
    }

    // The agent's leg of a number dialed from the soft phone, or of a consult, hung up.
    private async Task AgentLegEndedAsync(string agentLegId, TelnyxOutboundBridgeState state, CancellationToken cancellationToken)
    {
        if (state.IsConsultAgentLeg && state.Detached != true)
        {
            await EndConsultLegAsync(agentLegId, state, cancellationToken);

            return;
        }

        // A transfer or a consult is still under way for this call's party, and wants them: they are not released.
        if (!string.IsNullOrWhiteSpace(state.PendingTransferCallControlId) && state.Detached != true)
        {
            return;
        }

        await ReleaseRemotePartyAsync(state, cancellationToken);
    }

    private async Task EndConsultLegAsync(string consultLegId, TelnyxOutboundBridgeState consult, CancellationToken cancellationToken)
    {
        if (consult.TargetAnswered == true &&
            await _transfers.CompleteConsultAsync(consultLegId, consult, hangUpConsultLeg: false, cancellationToken))
        {
            return;
        }

        // Over before the destination took the call -- cancelled, never answered, or nobody left to hand over.
        await HangupLegAsync(consult.PeerCallControlId, cancellationToken);

        if (!await ReturnCallToAgentAsync(consult.ConsultOfCallControlId, consultLegId, cancellationToken))
        {
            // The agent's leg of the call is gone too, so its party would be alone on a parked line.
            await HangupLegAsync(consult.PartyCallControlId, cancellationToken);
        }
    }

    // Clears the transfer the agent's leg is waiting on, so the agent hanging up releases the party again. Returns
    // whether the agent's leg is still up.
    private async Task<bool> ReturnCallToAgentAsync(string agentLegId, string transferLegId, CancellationToken cancellationToken)
    {
        var (alive, agent) = await _transfers.ReadAsync(agentLegId, cancellationToken);

        if (alive &&
            agent is not null &&
            string.Equals(agent.PendingTransferCallControlId, transferLegId, StringComparison.Ordinal))
        {
            await _transfers.UpdateStateAsync(agentLegId, agent.WithPendingTransfer(null), cancellationToken);
        }

        return alive;
    }

    // The agent's consult leg answered: ring the colleague on a transfer leg that names it, to be joined in the
    // consult's conference when it answers.
    private Task<string> RingConsultTargetAsync(string consultLegId, TelnyxOutboundBridgeState consult, CancellationToken cancellationToken)
        => _transfers.RingAsync(consult.Destination, new TelnyxOutboundBridgeState
        {
            Intent = TelnyxOutboundBridgeState.TransferLegIntent,
            PeerCallControlId = consultLegId,
            TransferOfCallControlId = consult.ConsultOfCallControlId,
            ConferenceName = TelnyxTransferCommands.ConsultConferenceName(consultLegId),
            TargetUserId = consult.TargetUserId,
            PartyNumber = consult.PartyNumber,
            CallerDisplayName = consult.CallerDisplayName,
        }, cancellationToken);
}
