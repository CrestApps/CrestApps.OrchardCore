using CrestApps.Core.Support;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// The agent leg rung while a Contact Center offer is still ringing.
/// </summary>
public sealed partial class TelnyxOutboundBridgeOrchestrator
{
    private async Task AdvancePreDialedAgentLegAsync(
        TelnyxCallEvent callEvent,
        TelnyxOutboundBridgeState state,
        bool isAnswered,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(callEvent.CallControlId))
        {
            return;
        }

        if (isAnswered)
        {
            if (_preDialCoordinator is null)
            {
                // Nothing here can say whether the offer is still the agent's to take, and a leg that cannot be
                // decided is never joined to a caller.
                await HangupLegAsync(callEvent.CallControlId, cancellationToken);

                return;
            }

            await _preDialCoordinator.OnAgentLegAnsweredAsync(
                TelnyxConstants.ProviderTechnicalName,
                state.ReservationId,
                callEvent.CallControlId,
                cancellationToken);

            return;
        }

        if (!IsHangup(callEvent))
        {
            return;
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "A pre-dialed Contact Center agent leg ended. HangupCause={HangupCause}, SipHangupCause={SipHangupCause}, HangupSource={HangupSource}, ReservationId={ReservationId}.",
                callEvent.HangupCause.SanitizeLogValue(),
                callEvent.SipHangupCause.SanitizeLogValue(),
                callEvent.HangupSource.SanitizeLogValue(),
                state.ReservationId.SanitizeLogValue());
        }

        if (await TryRingPreDialedAgentAgainAsync(callEvent, state, cancellationToken))
        {
            // The agent's phone moved to another credential, and the offer now tracks the leg rung there.
            return;
        }

        if (_preDialCoordinator is not null)
        {
            // Unlike the accept-time agent leg, a pre-dialed leg ending is not by itself a failed connect: the offer
            // may simply have been declined or expired. The coordinator knows which, and fails the call only when the
            // offer was accepted and this leg was what was to carry the agent to the caller.
            await _preDialCoordinator.OnAgentLegEndedAsync(
                TelnyxConstants.ProviderTechnicalName,
                state.ReservationId,
                callEvent.CallControlId,
                ResolveAgentLegFailureCause(callEvent),
                cancellationToken);
        }

        // A leg that was joined to the caller carried the call, and its end is the call's end. The coordinator's record
        // of the leg outlives only its ring window, so a conversation longer than that is recognized from the call
        // itself; a leg that was never joined is left to the coordinator above.
        await RecordAgentLegEndedAsync(callEvent, state, cancellationToken);
    }
}
