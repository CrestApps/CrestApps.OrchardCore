namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Spoken commands: a message read to the caller, and a prompt that collects a key press.
/// </summary>
/// <remarks>
/// Telnyx lists <c>voice</c> as required on both <c>speak</c> and <c>gather_using_speak</c> and refuses a command
/// without it, which the person on the line hears as silence. A command whose caller names no voice or language is
/// spoken in the tenant's text-to-speech voice and language, so none is ever sent without one.
/// </remarks>
public sealed partial class TelnyxApiClient
{
    /// <summary>
    /// Speaks a message to the caller.
    /// </summary>
    /// <param name="callControlId">The leg to speak on.</param>
    /// <param name="text">What to say.</param>
    /// <param name="voice">The voice to say it in; the tenant's when none is given.</param>
    /// <param name="language">The language to say it in; the tenant's when none is given.</param>
    /// <param name="clientState">The opaque state echoed back on every event for the leg.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public Task<TelnyxApiResult> SpeakAsync(
        string callControlId,
        string text,
        string voice = null,
        string language = null,
        string clientState = null,
        CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object>(StringComparer.Ordinal) { ["payload"] = text };

        ApplySpeech(body, voice, language);

        if (!string.IsNullOrWhiteSpace(clientState))
        {
            body["client_state"] = EncodeClientState(clientState);
        }

        // Not retried: a retry that the provider had accepted would say the same thing to the caller twice.
        return PostActionAsync(callControlId, "speak", body, retryable: false, cancellationToken);
    }

    /// <summary>
    /// Speaks a prompt and collects a key press, in the tenant's voice and language.
    /// </summary>
    /// <param name="callControlId">The leg to prompt on.</param>
    /// <param name="text">The prompt.</param>
    /// <param name="validDigits">The keys that are accepted.</param>
    /// <param name="clientState">The opaque state echoed back on every event for the leg.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public Task<TelnyxApiResult> GatherAsync(
        string callControlId,
        string text,
        string validDigits,
        string clientState = null,
        CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["payload"] = text,
            ["valid_digits"] = validDigits,
            ["maximum_digits"] = 1,

            // Telnyx ends collection on '#' by default, which must not swallow a key the caller is asked to press.
            ["terminating_digit"] = TelnyxPrompts.PickTerminatingDigit(validDigits),
        };

        ApplySpeech(body, voice: null, language: null);

        if (!string.IsNullOrWhiteSpace(clientState))
        {
            body["client_state"] = EncodeClientState(clientState);
        }

        return PostActionAsync(callControlId, "gather_using_speak", body, retryable: false, cancellationToken);
    }

    private void ApplySpeech(Dictionary<string, object> body, string voice, string language)
    {
        body["voice"] = string.IsNullOrWhiteSpace(voice) ? TelnyxPrompts.ResolveVoice(_options) : voice;
        body["language"] = string.IsNullOrWhiteSpace(language) ? TelnyxPrompts.ResolveLanguage(_options) : language;
    }
}
