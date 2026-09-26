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
            if (_supervisorLegEventSink is not null && state.Detached != true)
            {
                await _supervisorLegEventSink.OnEndedAsync(
                    TelnyxConstants.ProviderTechnicalName,
                    state.PeerCallControlId,
                    callEvent.CallControlId,
                    callEvent.OccurredUtc,
                    ResolveAgentLegFailureCause(callEvent),
                    cancellationToken);
            }

            // A supervisor who hung up on their own phone leaves the call where it is; once nobody is listening it goes
            // back on its bridge. A leg the platform released (a stop, the call ending) was already dealt with there.
            if (state.Detached != true && _options.IsConfigured)
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
        var conferenceId = await _supervisedConference.EnsureAsync(state.PeerCallControlId, state.PartyCallControlId, cancellationToken);
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

        if (_supervisorLegEventSink is not null)
        {
            await _supervisorLegEventSink.OnAnsweredAsync(
                TelnyxConstants.ProviderTechnicalName,
                state.PeerCallControlId,
                supervisorLegId,
                cancellationToken);
        }
    }
}
