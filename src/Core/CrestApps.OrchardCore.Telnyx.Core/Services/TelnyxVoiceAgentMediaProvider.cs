using CrestApps.OrchardCore.Telephony.Services;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Carries an automated voice conversation over Telnyx. Everything provider-specific about automated voice is
/// here; the conversation loop itself knows none of it.
/// </summary>
public sealed class TelnyxVoiceAgentMediaProvider : IVoiceAgentMediaProvider
{
    private readonly TelnyxApiClient _apiClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxVoiceAgentMediaProvider"/> class.
    /// </summary>
    /// <param name="apiClient">The typed Telnyx client.</param>
    public TelnyxVoiceAgentMediaProvider(TelnyxApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    /// <inheritdoc/>
    public string TechnicalName => TelnyxConstants.ProviderTechnicalName;

    /// <inheritdoc/>
    /// <remarks>
    /// A neural voice rather than one of the basic voices: the difference between the two is most of what makes
    /// an automated call sound robotic. The account must support this voice for the speak command.
    /// </remarks>
    public string DefaultVoice => "AWS.Polly.Joanna-Neural";

    /// <inheritdoc/>
    public async Task<bool> SpeakAsync(
        string providerCallId,
        string text,
        string voice = null,
        string language = null,
        CancellationToken cancellationToken = default)
    {
        // A refusal is reported rather than thrown: the loop is mid-conversation with a real caller, and an
        // exception here abandons them silently.
        var result = await _apiClient.SpeakAsync(providerCallId, text, voice, language, cancellationToken: cancellationToken);

        return result.Succeeded;
    }

    /// <inheritdoc/>
    public async Task<bool> StartTranscriptionAsync(
        string providerCallId,
        string language = null,
        string commandId = null,
        CancellationToken cancellationToken = default)
    {
        var spokenLanguage = string.IsNullOrWhiteSpace(language) ? "en" : language;

        // A model made for phone audio, with numbers and addresses written the way they are meant. The provider's
        // default engine, untuned, heard a caller's answers as "who's", "GNC" and "on", read an email address back
        // wrong four times running, and turned a reply into "no I don't want anyone" -- which the review then took
        // as an opt-out. Everything the conversation does, it does with what this hears.
        var result = await StartWithAsync(providerCallId, commandId, new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["transcription_engine"] = "Deepgram",
            ["transcription_model"] = "deepgram/nova-3",
            ["language"] = spokenLanguage,
            ["smart_format"] = true,
        }, cancellationToken);

        if (result)
        {
            return true;
        }

        // An account that does not offer that engine refuses the command, and a call that is not listening is a
        // caller talking to nobody. The default engine, on its phone-call model, is the next best thing. A command
        // id is single-use, so the second attempt carries its own.
        return await StartWithAsync(
            providerCallId,
            string.IsNullOrWhiteSpace(commandId) ? null : commandId + "-fallback",
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["transcription_engine"] = "Google",
                ["model"] = "phone_call",
                ["use_enhanced"] = true,
                ["language"] = spokenLanguage,
            },
            cancellationToken);
    }

    private async Task<bool> StartWithAsync(
        string providerCallId,
        string commandId,
        Dictionary<string, object> engineConfig,
        CancellationToken cancellationToken)
    {
        var body = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["transcription_engine"] = engineConfig["transcription_engine"],
            ["transcription_engine_config"] = engineConfig,

            // The inbound track is the far end, so the assistant's own text-to-speech is never transcribed back as
            // if the person had said it.
            ["transcription_tracks"] = "inbound",
        };

        if (!string.IsNullOrWhiteSpace(commandId))
        {
            body["command_id"] = commandId;
        }

        var result = await _apiClient.PostCallActionAsync(providerCallId, "transcription_start", body, cancellationToken);

        return result.Succeeded;
    }

    /// <inheritdoc/>
    public async Task<bool> StopTranscriptionAsync(string providerCallId, CancellationToken cancellationToken = default)
    {
        var result = await _apiClient.PostCallActionAsync(providerCallId, "transcription_stop", body: null, cancellationToken);

        return result.Succeeded;
    }

    /// <inheritdoc/>
    public async Task<bool> GatherAsync(
        string providerCallId,
        string text,
        string validDigits,
        CancellationToken cancellationToken = default)
    {
        var result = await _apiClient.GatherAsync(providerCallId, text, validDigits, cancellationToken: cancellationToken);

        return result.Succeeded;
    }

    /// <inheritdoc/>
    public async Task<bool> HangupAsync(string providerCallId, CancellationToken cancellationToken = default)
    {
        var result = await _apiClient.HangupAsync(providerCallId, cancellationToken);

        return result.Succeeded;
    }
}
