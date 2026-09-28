namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Handles the Telnyx call-control events of an outbound leg the platform dialed for an automated AI voice
/// agent (client_state intent <see cref="TelnyxOutboundBridgeState.AiVoiceLegIntent"/>). Implementations drive
/// the conversation loop -- greeting, listening (transcription), replying (speak), and settling the activity on
/// hangup. Registered optionally so the base Telnyx feature carries no AI or omnichannel dependency.
/// </summary>
public interface ITelnyxAiVoiceEventHandler
{
    /// <summary>
    /// Advances the AI voice conversation for the leg the event belongs to.
    /// </summary>
    /// <param name="callEvent">The parsed Telnyx call event.</param>
    /// <param name="state">The AI-voice client state parsed from the leg, carrying the activity identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task HandleAsync(TelnyxCallEvent callEvent, TelnyxOutboundBridgeState state, CancellationToken cancellationToken = default);
}
