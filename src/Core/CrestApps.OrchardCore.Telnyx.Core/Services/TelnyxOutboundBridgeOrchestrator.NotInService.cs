using CrestApps.Core.Support;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// The last message an agent hears when the number they dialed from the soft phone is not in service.
/// </summary>
public sealed partial class TelnyxOutboundBridgeOrchestrator
{
    internal const string NotInServiceNotice = "The number you dialed is not in service. Please check the number and try again.";

    private static bool IsSpeakEnded(TelnyxCallEvent callEvent)
        => string.Equals(callEvent.EventType?.Trim(), "call.speak.ended", StringComparison.OrdinalIgnoreCase);

    // The agent's leg is answered before the number is dialed and only bridged once the number answers, so a number
    // that is not in service ends with the agent hearing nothing at all: the carrier's announcement plays on the
    // number's leg, which is never joined to the agent. Says so on the agent's leg instead, which is hung up when the
    // message ends. Returns false when the message could not be started, and the leg is then hung up as before.
    private async Task<bool> AnnounceNotInServiceAsync(string agentLegCallControlId, TelnyxCallEvent callEvent, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(agentLegCallControlId))
        {
            return false;
        }

        try
        {
            var (isAlive, agentState) = await _transfers.ReadAsync(agentLegCallControlId, cancellationToken);

            if (!isAlive || agentState is null || agentState.Intent != TelnyxOutboundBridgeState.AgentLegIntent)
            {
                return false;
            }

            // The speak command's state replaces the leg's, so the leg keeps everything it carried and only learns
            // that it ends with this message.
            var spoken = await _apiClient.SpeakAsync(
                agentLegCallControlId,
                NotInServiceNotice,
                clientState: agentState.AsHangUpAfterNotice().ToClientStateJson(),
                cancellationToken: cancellationToken);

            if (!spoken.Succeeded)
            {
                _logger.LogWarning(
                    "Telnyx refused to tell agent leg {AgentLeg} that the number is not in service ({StatusCode}); hanging it up instead.",
                    agentLegCallControlId.SanitizeLogValue(),
                    spoken.StatusCode);

                return false;
            }

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "The number dialed on agent leg {AgentLeg} is not in service (cause {HangupCause}, SIP {SipHangupCause}); told the agent before hanging up.",
                    agentLegCallControlId.SanitizeLogValue(),
                    callEvent.HangupCause.SanitizeLogValue(),
                    callEvent.SipHangupCause.SanitizeLogValue());
            }

            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "An error occurred while telling agent leg {AgentLeg} that the number is not in service.", agentLegCallControlId.SanitizeLogValue());

            return false;
        }
    }
}
