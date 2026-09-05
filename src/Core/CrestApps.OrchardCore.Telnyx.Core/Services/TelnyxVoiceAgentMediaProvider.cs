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
        var body = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            // Engine "A" is the Google-backed transcription. The inbound track is the far end, so the assistant's
            // own text-to-speech is never transcribed back as if the person had said it.
            ["transcription_engine"] = "A",
            ["transcription_tracks"] = "inbound",
            ["language"] = "en",
        };

        if (!string.IsNullOrWhiteSpace(language))
        {
            body["language"] = language;
        }

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
