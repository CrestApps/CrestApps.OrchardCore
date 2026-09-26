using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// The interaction status a provider's call state implies. Live ingestion and reconciliation both project the call
/// session onto its interaction, and they have to agree: if one path settled an interaction as failed and the other
/// as ended, a repair pass would try to move it between two settled states and be refused.
/// </summary>
/// <remarks>
/// <see cref="InteractionStatus.Failed"/> is kept for a call the provider could not carry. A caller who hangs up
/// before anybody answers cancelled the call; the provider reports that as <see cref="VoiceCallState.Canceled"/>, and
/// it is an abandon, which the reports read off an ended interaction, not a technical failure. A call nobody picked up
/// (<see cref="VoiceCallState.NoAnswer"/>) or that was refused as busy (<see cref="VoiceCallState.Rejected"/>) stays
/// failed: those are what an outbound attempt's dial outcome is made of.
/// </remarks>
internal static class VoiceCallInteractionStatus
{
    /// <summary>
    /// Gets the interaction status a call state implies.
    /// </summary>
    /// <param name="state">The provider's call state.</param>
    /// <returns>The interaction status.</returns>
    public static InteractionStatus From(VoiceCallState state)
        => state switch
        {
            VoiceCallState.Planned => InteractionStatus.Created,
            VoiceCallState.Dialing => InteractionStatus.Ringing,
            VoiceCallState.Ringing => InteractionStatus.Ringing,
            VoiceCallState.Connected => InteractionStatus.Connected,
            VoiceCallState.OnHold => InteractionStatus.Held,
            VoiceCallState.Ending => InteractionStatus.Connected,
            VoiceCallState.Transferred => InteractionStatus.Transferring,
            VoiceCallState.Ended => InteractionStatus.Ended,
            VoiceCallState.Canceled => InteractionStatus.Ended,
            VoiceCallState.Failed => InteractionStatus.Failed,
            VoiceCallState.NoAnswer => InteractionStatus.Failed,
            VoiceCallState.Rejected => InteractionStatus.Failed,
            _ => InteractionStatus.Created,
        };

    /// <summary>
    /// Gets the settled interaction status a terminal call state implies.
    /// </summary>
    /// <param name="state">The provider's call state, if there is one.</param>
    /// <param name="status">The settled interaction status, when the call state is terminal.</param>
    /// <returns><see langword="true"/> when the call state is terminal; otherwise <see langword="false"/>.</returns>
    public static bool TryGetSettled(VoiceCallState? state, out InteractionStatus status)
    {
        if (state is VoiceCallState.Ended or VoiceCallState.Canceled or VoiceCallState.Failed or VoiceCallState.NoAnswer or VoiceCallState.Rejected)
        {
            status = From(state.Value);

            return true;
        }

        status = default;

        return false;
    }
}
