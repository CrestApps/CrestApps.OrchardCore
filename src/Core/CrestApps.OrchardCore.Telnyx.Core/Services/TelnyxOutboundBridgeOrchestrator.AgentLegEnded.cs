using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// A Contact Center agent leg hanging up while it is joined to the caller.
/// </summary>
public sealed partial class TelnyxOutboundBridgeOrchestrator
{
    /// <summary>
    /// Tells the Contact Center that the agent's leg hung up, dated when the provider says it did.
    /// </summary>
    /// <remarks>
    /// The leg's identifier belongs to no interaction, so normalization discards its hangup. The bridge was made on the
    /// agent's leg, and Telnyx does not reliably release the caller when that leg goes: the caller stayed on a silent
    /// line and the call, its talk and hold time and the agent's wrap-up all waited for the caller's own hangup. The
    /// Contact Center decides whether the leg was still what carried the call (it ignores a leg that was never joined,
    /// one the call has moved on from, and one whose call already ended), and ends the call and releases the caller
    /// when it was.
    /// </remarks>
    private async Task RecordAgentLegEndedAsync(
        TelnyxCallEvent callEvent,
        TelnyxOutboundBridgeState state,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(state.PeerCallControlId) || string.IsNullOrWhiteSpace(callEvent.CallControlId))
        {
            return;
        }

        await _agentLegFailureService.RecordEndedAsync(
            TelnyxConstants.ProviderTechnicalName,
            state.PeerCallControlId,
            callEvent.CallControlId,
            callEvent.OccurredUtc,
            ResolveAgentLegFailureCause(callEvent) ?? HangupCause.NormalClearing,
            cancellationToken);
    }
}
