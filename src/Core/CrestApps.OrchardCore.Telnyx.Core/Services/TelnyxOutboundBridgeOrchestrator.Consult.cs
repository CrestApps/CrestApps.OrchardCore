using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// The leg a warm transfer rings to the destination the agent is consulting.
/// </summary>
public sealed partial class TelnyxOutboundBridgeOrchestrator
{
    private async Task<TelnyxOutboundBridgeLeg> AdvanceConsultLegAsync(
        TelnyxCallEvent callEvent,
        TelnyxOutboundBridgeState state,
        bool isAnswered,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(callEvent.CallControlId) ||
            string.IsNullOrWhiteSpace(state.PeerCallControlId) ||
            string.IsNullOrWhiteSpace(state.ConsultId))
        {
            return TelnyxOutboundBridgeLeg.DestinationLeg;
        }

        if (isAnswered)
        {
            // The destination picked up: put them in the conference where the agent is waiting and the customer is
            // held, then tell the Contact Center the transfer can be completed.
            await JoinConsultConferenceAsync(callEvent.CallControlId, state, cancellationToken);

            if (_consultLegEventSink is not null)
            {
                await _consultLegEventSink.OnAnsweredAsync(
                    TelnyxConstants.ProviderTechnicalName,
                    state.PeerCallControlId,
                    state.ConsultId,
                    callEvent.CallControlId,
                    cancellationToken);
            }

            return TelnyxOutboundBridgeLeg.DestinationLeg;
        }

        if (!IsHangup(callEvent) || _consultLegEventSink is null)
        {
            return TelnyxOutboundBridgeLeg.DestinationLeg;
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "A consult leg ended. HangupCause={HangupCause}, SipHangupCause={SipHangupCause}, ConsultId={ConsultId}, CustomerCallControlId={CustomerCallControlId}.",
                callEvent.HangupCause.SanitizeLogValue(),
                callEvent.SipHangupCause.SanitizeLogValue(),
                state.ConsultId.SanitizeLogValue(),
                state.PeerCallControlId.SanitizeLogValue());
        }

        var outcome = await _consultLegEventSink.OnEndedAsync(
            TelnyxConstants.ProviderTechnicalName,
            state.PeerCallControlId,
            state.ConsultId,
            callEvent.CallControlId,
            ResolveAgentLegFailureCause(callEvent),
            callEvent.OccurredUtc,
            cancellationToken);

        if (outcome == ConsultLegEndedOutcome.ReleaseCustomer)
        {
            // The external party the call was handed to hung up. The call left the contact center at the handover,
            // so nothing else is following the customer's leg to release it.
            await HangupLegAsync(state.PeerCallControlId, cancellationToken);
        }

        // An internal leg: its events belong to no interaction and never surface on anybody's phone as a call.
        return TelnyxOutboundBridgeLeg.DestinationLeg;
    }

    private async Task JoinConsultConferenceAsync(string consultLegId, TelnyxOutboundBridgeState state, CancellationToken cancellationToken)
    {
        if (!_options.IsConfigured)
        {
            return;
        }

        var conferenceName = string.IsNullOrWhiteSpace(state.ConferenceName)
            ? TelnyxContactCenterVoiceProvider.ConsultConferenceName(state.ConsultId)
            : state.ConferenceName;

        var conference = await _apiClient.FindConferenceByNameAsync(conferenceName, cancellationToken);

        if (!conference.Succeeded || string.IsNullOrWhiteSpace(conference.ConferenceId))
        {
            _logger.LogError(
                "The consult conference '{ConferenceName}' could not be found to join the answered destination.",
                conferenceName.SanitizeLogValue());

            return;
        }

        var join = await _apiClient.JoinConferenceAsync(
            conference.ConferenceId,
            consultLegId,
            endConferenceOnExit: false,
            commandId: $"cc-consult-join-{consultLegId}",
            cancellationToken);

        if (!join.Succeeded)
        {
            _logger.LogError(
                "Telnyx rejected joining the consulted destination to conference '{ConferenceName}' with status code {StatusCode}. Response: {Response}",
                conferenceName.SanitizeLogValue(),
                join.StatusCode,
                join.ErrorBody.SanitizeLogValue());
        }
    }
}
