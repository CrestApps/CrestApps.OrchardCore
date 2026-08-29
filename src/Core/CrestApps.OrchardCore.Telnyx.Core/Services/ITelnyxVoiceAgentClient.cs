namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// A thin Telnyx Call Control client for driving an automated AI voice agent over a single outbound call:
/// originating the call, speaking synthesized prompts, transcribing the caller, and hanging up. Each command
/// is idempotent against Telnyx webhook redelivery via a caller-supplied <c>command_id</c>.
/// </summary>
public interface ITelnyxVoiceAgentClient
{
    /// <summary>
    /// Originates an outbound call for the AI voice agent and returns the new leg's call-control id.
    /// </summary>
    /// <param name="to">The destination address (the customer's phone number).</param>
    /// <param name="from">The caller id to present, or <see langword="null"/> to use the configured default.</param>
    /// <param name="clientState">The AI-voice client state Telnyx echoes back on every event for the leg.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The originated call's call-control id, or <see langword="null"/> when origination failed.</returns>
    Task<string> OriginateAsync(string to, string from, TelnyxOutboundBridgeState clientState, CancellationToken cancellationToken = default);

    /// <summary>
    /// Speaks the given text on the call using Telnyx text-to-speech.
    /// </summary>
    Task SpeakAsync(string callControlId, string text, string voice, string language, string commandId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts real-time transcription of the caller's speech, emitting <c>call.transcription</c> events.
    /// </summary>
    Task StartTranscriptionAsync(string callControlId, string language, string commandId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops real-time transcription while the agent is speaking or the call is ending.
    /// </summary>
    Task StopTranscriptionAsync(string callControlId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hangs up the call.
    /// </summary>
    Task HangupAsync(string callControlId, CancellationToken cancellationToken = default);
}
