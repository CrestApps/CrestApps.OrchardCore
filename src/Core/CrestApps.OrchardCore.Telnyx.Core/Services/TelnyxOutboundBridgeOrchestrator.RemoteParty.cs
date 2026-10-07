using CrestApps.Core.Support;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Telling the agent's soft phone where the party of a number it dialed (or a colleague it called) stands.
/// </summary>
/// <remarks>
/// The agent's leg is answered by the soft phone before the number is dialed, and joined to it only once the number
/// answers, so the carrier's ringback plays on the number's leg and never reaches the agent: the agent heard silence
/// and could not tell whether anything was being dialed. The agent's leg reports nothing while the number rings, so the
/// phone is told here -- ringing once the number is dialed, answered once it is joined, ended once it goes without being
/// joined -- and plays a ringback tone of its own in between. A consult is left alone: it has its own status.
/// </remarks>
public sealed partial class TelnyxOutboundBridgeOrchestrator
{
    // The leg of a number the agent dialed, or of a colleague they called: not a number a consult dialed, whose answer and
    // hang-up are the consult's, nor a party already handed on to somebody else.
    private static bool IsAgentsOwnDial(TelnyxOutboundBridgeState destinationState)
        => destinationState.Detached != true && string.IsNullOrWhiteSpace(destinationState.TransferOfCallControlId);

    private async Task NotifyRemotePartyAsync(string agentLegCallControlId, RemotePartyState state, CancellationToken cancellationToken)
    {
        if (_remotePartyNotifier is null || string.IsNullOrWhiteSpace(agentLegCallControlId))
        {
            return;
        }

        try
        {
            await _remotePartyNotifier.NotifyAsync(TelnyxConstants.ProviderTechnicalName, agentLegCallControlId, state, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Only the agent's ringback depends on this; the call itself is unaffected.
            _logger.LogWarning(
                ex,
                "Could not tell the soft phone on agent leg {AgentLeg} that its party is {RemotePartyState}.",
                agentLegCallControlId.SanitizeLogValue(),
                state);
        }
    }
}
