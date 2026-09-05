namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// The four things an automated voice conversation needs a telephony provider to do: say something, listen,
/// collect a key press, and hang up.
/// <para>
/// The conversation loop itself — rendering the prompt, running the completion, deciding whether to hand off,
/// concluding the call — is the same whichever provider carries the audio, which is why it does not belong in a
/// provider module. This is the seam: everything provider-specific is behind it, and a second provider can offer
/// automated voice by implementing four methods rather than copying a thousand-line handler.
/// </para>
/// </summary>
public interface IVoiceAgentMediaProvider
{
    /// <summary>
    /// Gets the technical name of the provider this implementation speaks for, so a tenant with more than one
    /// provider resolves the right media for the call in hand.
    /// </summary>
    string TechnicalName { get; }

    /// <summary>
    /// Gets the text-to-speech voice used when the activity names none. Which voices exist, and what they are
    /// called, is a fact about the provider, so the default belongs here rather than in the conversation. A
    /// natural-sounding voice is the single biggest lever against a robotic delivery.
    /// </summary>
    string DefaultVoice { get; }

    /// <summary>
    /// Speaks a message to the caller.
    /// </summary>
    /// <param name="providerCallId">The provider's identifier for the caller's leg.</param>
    /// <param name="text">What to say.</param>
    /// <param name="voice">The voice to say it in, when the tenant has chosen one.</param>
    /// <param name="language">The language to say it in.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the provider accepted the command.</returns>
    Task<bool> SpeakAsync(
        string providerCallId,
        string text,
        string voice = null,
        string language = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts transcribing what the caller says.
    /// </summary>
    /// <param name="providerCallId">The provider's identifier for the caller's leg.</param>
    /// <param name="language">The language to transcribe in.</param>
    /// <param name="commandId">An idempotency key, so a redelivered command does not start a second transcription.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<bool> StartTranscriptionAsync(
        string providerCallId,
        string language = null,
        string commandId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops transcribing, so the caller is not still being listened to while the assistant is speaking.
    /// </summary>
    /// <param name="providerCallId">The provider's identifier for the caller's leg.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<bool> StopTranscriptionAsync(string providerCallId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Speaks a prompt and collects a single key press.
    /// </summary>
    /// <param name="providerCallId">The provider's identifier for the caller's leg.</param>
    /// <param name="text">The prompt.</param>
    /// <param name="validDigits">The keys that are accepted.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<bool> GatherAsync(
        string providerCallId,
        string text,
        string validDigits,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ends the call.
    /// </summary>
    /// <param name="providerCallId">The provider's identifier for the caller's leg.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<bool> HangupAsync(string providerCallId, CancellationToken cancellationToken = default);
}
