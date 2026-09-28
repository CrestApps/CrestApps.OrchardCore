using CrestApps.Core.Support;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// The leg a supervisor's own soft phone answers to listen to, coach or join a Contact Center call.
/// </summary>
/// <remarks>
/// The call is moved from its bridge into a conference only once the supervisor has actually answered, so a supervisor
/// who never picks up never disturbs the call. Its events are the platform's own and are never normalized as a call.
/// </remarks>
public sealed partial class TelnyxOutboundBridgeOrchestrator
{
    private async Task<TelnyxOutboundBridgeLeg> AdvanceSupervisorLegAsync(
        TelnyxCallEvent callEvent,
        TelnyxOutboundBridgeState state,
        bool isAnswered,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(callEvent.CallControlId) || string.IsNullOrWhiteSpace(state.PeerCallControlId))
        {
            return TelnyxOutboundBridgeLeg.DestinationLeg;
        }

        if (isAnswered && _options.IsConfigured)
        {
            await JoinSupervisorAsync(callEvent.CallControlId, state, cancellationToken);
        }
        else if (IsHangup(callEvent))
        {
            LogSupervisorLegEnded(callEvent, state);

            // A leg rung for a change of mode that ends -- answered or not -- takes the leg it was to replace with it, in
            // that leg's own state: whichever of the two the engagement names, its end is reported, so the engagement never
            // outlives both. Once the new leg has answered, the replaced one is already gone and this changes nothing.
            if (!string.IsNullOrWhiteSpace(state.ReplacesCallControlId) && _options.IsConfigured)
            {
                await HangupLegAsync(state.ReplacesCallControlId, cancellationToken);
            }

            // A leg the platform released itself (a stop, a transfer, the call ending) was recorded by whatever released
            // it. Reporting it again wrote the call's record a second time while the stop was still writing it, and the
            // stop failed on the conflict (live: POST dashboard/stop answered 500 with a ConcurrencyException).
            if (state.Detached != true)
            {
                // The first sink that knows the leg handles it: a Contact Center call's, or an agent's own phone call's.
                foreach (var sink in _supervisorLegEventSinks)
                {
                    if (await sink.OnEndedAsync(
                        TelnyxConstants.ProviderTechnicalName,
                        state.PeerCallControlId,
                        callEvent.CallControlId,
                        callEvent.OccurredUtc,
                        ResolveAgentLegFailureCause(callEvent),
                        cancellationToken))
                    {
                        break;
                    }
                }
            }

            // A supervisor who hung up on their own phone leaves the call where it is; once nobody is listening it goes
            // back on its bridge. A leg the platform released (a stop, the call ending) was already dealt with there, and a
            // call supervised where it is was never moved.
            if (state.Detached != true && state.SupervisesInPlace != true && _options.IsConfigured)
            {
                await _supervisedConference.RestoreIfUnsupervisedAsync(
                    state.PeerCallControlId,
                    state.PartyCallControlId,
                    callEvent.CallControlId,
                    cancellationToken);
            }
        }

        return TelnyxOutboundBridgeLeg.DestinationLeg;
    }

    private async Task JoinSupervisorAsync(string supervisorLegId, TelnyxOutboundBridgeState state, CancellationToken cancellationToken)
    {
        // The takeover that rang it is waiting for this answer and bridges the customer to the leg itself.
        if (state.TakesOver == true)
        {
            return;
        }

        if (state.SupervisesInPlace == true)
        {
            await AttachedSupervisorAnsweredAsync(supervisorLegId, state, cancellationToken);

            return;
        }

        var conferenceId = TelnyxSupervisedConference.IsOwnConference(state.ConferenceName, state.PeerCallControlId)
            ? await _supervisedConference.EnsureAsync(state.PeerCallControlId, state.PartyCallControlId, cancellationToken)
            : await _supervisedConference.FindRunningAsync(state.ConferenceName, cancellationToken);
        var role = string.IsNullOrWhiteSpace(state.SupervisorRole) ? "monitor" : state.SupervisorRole;

        if (string.IsNullOrWhiteSpace(conferenceId) ||
            !await _supervisedConference.JoinSupervisorAsync(conferenceId, supervisorLegId, role, state.PartyCallControlId, cancellationToken))
        {
            // The supervisor cannot be put on the call. Their leg is hung up, and its hang-up ends the engagement.
            _logger.LogWarning(
                "Supervisor leg '{SupervisorLegId}' could not join call '{CustomerLegId}'; it is hung up.",
                supervisorLegId.SanitizeLogValue(),
                state.PeerCallControlId.SanitizeLogValue());

            await HangupLegAsync(supervisorLegId, cancellationToken);

            return;
        }

        await ReportSupervisorAnsweredAsync(supervisorLegId, state, cancellationToken);
    }

    // Telnyx attached the answered leg to the agent's itself, in the role it was dialed with. It is never switched: live,
    // a leg dialed as barge and switched to listen the moment it answered carried nothing but silence to the supervisor. A
    // leg rung for a change of mode takes over from the one it replaces, which goes quietly now: its hang-up is not the
    // supervisor walking away.
    private async Task AttachedSupervisorAnsweredAsync(string supervisorLegId, TelnyxOutboundBridgeState state, CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Supervisor leg '{SupervisorLegId}' answered on agent leg '{AgentLegId}' as {Role}{Replacing}.",
                supervisorLegId.SanitizeLogValue(),
                state.PartyCallControlId.SanitizeLogValue(),
                (string.IsNullOrWhiteSpace(state.SupervisorRole) ? "monitor" : state.SupervisorRole).SanitizeLogValue(),
                string.IsNullOrWhiteSpace(state.ReplacesCallControlId) ? string.Empty : $", replacing '{state.ReplacesCallControlId.SanitizeLogValue()}'");
        }

        if (!string.IsNullOrWhiteSpace(state.ReplacesCallControlId))
        {
            var released = await _apiClient.HangupWithStateAsync(
                state.ReplacesCallControlId,
                new TelnyxOutboundBridgeState
                {
                    Intent = TelnyxOutboundBridgeState.ContactCenterSupervisorLegIntent,
                    PeerCallControlId = state.PeerCallControlId,
                    PartyCallControlId = state.PartyCallControlId,
                    SupervisesInPlace = true,
                    RingUserId = state.RingUserId,
                    Detached = true,
                }.ToClientStateJson(),
                cancellationToken);

            if (!released.Succeeded && !TelnyxApiErrors.IsCallAlreadyEnded(released))
            {
                _logger.LogWarning(
                    "Telnyx returned {StatusCode} releasing supervisor leg '{ReplacedLegId}' after '{SupervisorLegId}' took over from it. Response: {Response}",
                    released.StatusCode,
                    state.ReplacesCallControlId.SanitizeLogValue(),
                    supervisorLegId.SanitizeLogValue(),
                    released.ErrorBody.SanitizeLogValue());
            }
        }

        await ReportSupervisorAnsweredAsync(supervisorLegId, state, cancellationToken);
    }

    // Live, every mode a supervisor picked failed the same way -- the leg was refused at once (SIP 480) because their phone
    // was not registered on the credential it was rung at -- and nothing in the log said so beyond the raw webhook. A
    // refusal is named as what it most likely means.
    private void LogSupervisorLegEnded(TelnyxCallEvent callEvent, TelnyxOutboundBridgeState state)
    {
        if (state.Detached == true)
        {
            return;
        }

        var sipCause = callEvent.SipHangupCause?.Trim();

        if (int.TryParse(sipCause, out var sipCode) && sipCode >= 400)
        {
            _logger.LogWarning(
                "The phone of supervisor '{SupervisorUserId}' refused supervisor leg '{SupervisorLegId}' ({Role}) on call '{CustomerLegId}' with SIP {SipCode} ({HangupCause}). A 480 or 404 means the phone is not registered on the credential it was rung at: it should be closed and reopened.",
                state.RingUserId.SanitizeLogValue(),
                callEvent.CallControlId.SanitizeLogValue(),
                (state.SupervisorRole ?? "monitor").SanitizeLogValue(),
                state.PeerCallControlId.SanitizeLogValue(),
                sipCode,
                callEvent.HangupCause.SanitizeLogValue());

            return;
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Supervisor leg '{SupervisorLegId}' ({Role}) of supervisor '{SupervisorUserId}' on call '{CustomerLegId}' ended ({HangupCause}, SIP {SipCause}).",
                callEvent.CallControlId.SanitizeLogValue(),
                (state.SupervisorRole ?? "monitor").SanitizeLogValue(),
                state.RingUserId.SanitizeLogValue(),
                state.PeerCallControlId.SanitizeLogValue(),
                callEvent.HangupCause.SanitizeLogValue(),
                sipCause.SanitizeLogValue());
        }
    }

    private async Task ReportSupervisorAnsweredAsync(string supervisorLegId, TelnyxOutboundBridgeState state, CancellationToken cancellationToken)
    {
        foreach (var sink in _supervisorLegEventSinks)
        {
            if (await sink.OnAnsweredAsync(
                TelnyxConstants.ProviderTechnicalName,
                state.PeerCallControlId,
                supervisorLegId,
                cancellationToken))
            {
                break;
            }
        }
    }
}
