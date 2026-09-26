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

    // Telnyx attached the answered leg to the agent's itself. The mode may have changed while it rang, which changed only
    // the role it carries: it is given that role now.
    private async Task AttachedSupervisorAnsweredAsync(string supervisorLegId, TelnyxOutboundBridgeState state, CancellationToken cancellationToken)
    {
        var role = string.IsNullOrWhiteSpace(state.SupervisorRole) ? "monitor" : state.SupervisorRole;
        var switched = await _apiClient.SwitchSupervisorRoleAsync(supervisorLegId, role, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Supervisor leg '{SupervisorLegId}' answered on agent leg '{AgentLegId}' as {Role}; Telnyx returned {StatusCode} setting its role. Response: {Response}",
                supervisorLegId.SanitizeLogValue(),
                state.PartyCallControlId.SanitizeLogValue(),
                role.SanitizeLogValue(),
                switched.StatusCode,
                switched.ErrorBody.SanitizeLogValue());
        }

        await ReportSupervisorAnsweredAsync(supervisorLegId, state, cancellationToken);
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
