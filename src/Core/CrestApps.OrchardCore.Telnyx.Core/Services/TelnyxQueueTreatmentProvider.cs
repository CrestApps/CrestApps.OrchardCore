using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Makes a waiting caller on a Telnyx leg hear what the queue's treatment policy decided is due: a spoken update,
/// hold music, a ringing tone, or a prompt that collects a key press.
/// </summary>
/// <remarks>
/// Nothing here throws. Treatment runs on a timer across every waiting caller, and a leg that has just hung up
/// would otherwise take the sweep down and leave everybody else in silence.
/// <para>
/// Every spoken command carries the tenant's voice and language: Telnyx lists <c>voice</c> as required on both
/// <c>speak</c> and <c>gather_using_speak</c> and refuses the command without it, so the callback offer used to be
/// refused outright and the caller never heard it.
/// </para>
/// </remarks>
public sealed class TelnyxQueueTreatmentProvider : IQueueTreatmentProvider
{
    private readonly TelnyxApiClient _apiClient;
    private readonly IVoiceMediaItemManager _voiceMediaItemManager;
    private readonly IOptionsMonitor<TelnyxOptions> _options;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxQueueTreatmentProvider"/> class.
    /// </summary>
    /// <param name="apiClient">The typed Telnyx client.</param>
    /// <param name="voiceMediaItemManager">The voice media catalog, used to resolve a clip to its provider name.</param>
    /// <param name="options">The tenant's Telnyx options, for the voice and language prompts are spoken in.</param>
    /// <param name="logger">The logger.</param>
    public TelnyxQueueTreatmentProvider(
        TelnyxApiClient apiClient,
        IVoiceMediaItemManager voiceMediaItemManager,
        IOptionsMonitor<TelnyxOptions> options,
        ILogger<TelnyxQueueTreatmentProvider> logger)
    {
        _apiClient = apiClient;
        _voiceMediaItemManager = voiceMediaItemManager;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task SpeakAsync(string providerCallId, string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerCallId) || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var options = _options.CurrentValue;
        var result = await _apiClient.SpeakAsync(
            providerCallId,
            text,
            TelnyxPrompts.ResolveVoice(options),
            TelnyxPrompts.ResolveLanguage(options),
            cancellationToken: cancellationToken);

        Report(result.Succeeded, "speak", providerCallId);
    }

    /// <inheritdoc/>
    public async Task StartHoldMusicAsync(string providerCallId, string mediaId, CancellationToken cancellationToken = default)
    {
        // A queue with no hold media wants silence. Asking the provider to play nothing logs an error every time
        // somebody waits in it.
        if (string.IsNullOrWhiteSpace(providerCallId) || string.IsNullOrWhiteSpace(mediaId))
        {
            return;
        }

        var audio = await ResolveAudioAsync(mediaId, cancellationToken);

        if (string.IsNullOrEmpty(audio))
        {
            return;
        }

        // Looped: music that plays once leaves the caller in silence for the rest of their wait, which sounds
        // exactly like a call that has dropped.
        var result = await _apiClient.PlaybackAsync(providerCallId, audio, loop: true, cancellationToken);

        Report(result.Succeeded, "playback_start", providerCallId);
    }

    /// <inheritdoc/>
    public async Task StartRingbackAsync(string providerCallId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerCallId))
        {
            return;
        }

        // Sent as the audio itself rather than a URL or a stored clip, so it needs nothing hosted or uploaded; looped
        // until the agent is bridged in (the bridge stops the caller's playback) or the caller is sent elsewhere
        // (leaving the queue stops it).
        var result = await _apiClient.PostCallActionAsync(
            providerCallId,
            "playback_start",
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["playback_content"] = TelnyxRingbackTone.Base64Wav,
                ["loop"] = "infinity",
            },
            cancellationToken);

        Report(result.Succeeded, "playback_start", providerCallId);
    }

    /// <inheritdoc/>
    public async Task StopHoldMusicAsync(string providerCallId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerCallId))
        {
            return;
        }

        var result = await _apiClient.StopPlaybackAsync(providerCallId, cancellationToken);

        Report(result.Succeeded, "playback_stop", providerCallId);
    }

    /// <inheritdoc/>
    public async Task OfferChoiceAsync(string providerCallId, string text, string acceptKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerCallId) || string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(acceptKey))
        {
            return;
        }

        var body = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["payload"] = text,
            ["payload_type"] = "text",

            // Any key is collected, not only the one that accepts: a caller who presses something else has answered
            // "no", and is put straight back to their music rather than left to wait out the timeout in silence.
            ["valid_digits"] = "0123456789*#",
            ["minimum_digits"] = 1,
            ["maximum_digits"] = 1,

            // The key that accepts must never be the one that ends collection with nothing collected.
            ["terminating_digit"] = TelnyxPrompts.PickTerminatingDigit(acceptKey),

            // Asked once. Telnyx replays the prompt up to three times by default, which to a caller who has already
            // decided to keep waiting is the queue nagging them with their music off.
            ["maximum_tries"] = 1,
            ["timeout_millis"] = TelnyxConstants.Gather.TimeoutMillis,
        };

        TelnyxPrompts.ApplySpeech(body, _options.CurrentValue);

        var result = await _apiClient.PostCallActionAsync(providerCallId, "gather_using_speak", body, cancellationToken);

        Report(result.Succeeded, "gather_using_speak", providerCallId);
    }

    /// <inheritdoc/>
    public async Task EndWithMessageAsync(string providerCallId, string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerCallId))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(text))
        {
            var options = _options.CurrentValue;

            // The hang-up is issued when Telnyx reports the speech ended (call.speak.ended carries this state back),
            // so the caller hears the whole message.
            var spoken = await _apiClient.SpeakAsync(
                providerCallId,
                text,
                TelnyxPrompts.ResolveVoice(options),
                TelnyxPrompts.ResolveLanguage(options),
                TelnyxCallFlowClientState.ForHangUpAfterSpeech().ToJson(),
                cancellationToken);

            if (spoken.Succeeded)
            {
                return;
            }

            Report(spoken.Succeeded, "speak", providerCallId);
        }

        // Nothing could be said, so nothing will report that it finished: the call is ended now rather than left open
        // on a silent line.
        var hungUp = await _apiClient.HangupAsync(providerCallId, cancellationToken);

        Report(hungUp.Succeeded, "hangup", providerCallId);
    }

    /// <summary>
    /// Turns whatever the queue was configured with into something Telnyx will actually play.
    /// </summary>
    /// <remarks>
    /// The queue stores the identifier of a clip in the voice media catalog, and that identifier means nothing to
    /// the provider — the clip is held under a name Telnyx assigned when it was uploaded. Passing the catalog id
    /// straight through was refused by Telnyx and produced silence on the line with nothing logged above debug,
    /// so the queue looked correctly configured and the caller heard nothing.
    /// <para>
    /// A value that is already a URL is passed through, so a queue can still point at externally hosted audio
    /// without going through the catalog.
    /// </para>
    /// </remarks>
    /// <param name="mediaId">The configured hold-music value.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    private async Task<string> ResolveAudioAsync(string mediaId, CancellationToken cancellationToken)
    {
        if (Uri.TryCreate(mediaId, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return mediaId;
        }

        var item = await _voiceMediaItemManager.FindByIdAsync(mediaId, cancellationToken);

        if (item is null)
        {
            _logger.LogWarning(
                "Queue hold music refers to voice media '{MediaId}', which is not in the catalog; the caller waits in silence.",
                mediaId.SanitizeLogValue());

            return null;
        }

        if (string.IsNullOrWhiteSpace(item.MediaReference))
        {
            _logger.LogWarning(
                "Voice media '{MediaId}' has no provider reference, so it was never stored on the provider; the caller waits in silence.",
                mediaId.SanitizeLogValue());

            return null;
        }

        return item.MediaReference;
    }

    private void Report(bool succeeded, string action, string providerCallId)
    {
        if (succeeded || !_logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        // Debug, not warning: the overwhelmingly common cause is a caller who hung up between the sweep reading
        // them and the command reaching the provider, and that is not an operator's problem.
        _logger.LogDebug(
            "The Telnyx '{Action}' queue-treatment command was refused for call '{CallId}'; the caller has most likely already left the queue.",
            action.SanitizeLogValue(),
            providerCallId.SanitizeLogValue());
    }
}
