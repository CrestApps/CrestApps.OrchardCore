using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Makes a waiting caller on a Telnyx leg hear what the queue's treatment policy decided is due: a spoken update,
/// hold music, or a prompt that collects a key press.
/// </summary>
/// <remarks>
/// Nothing here throws. Treatment runs on a timer across every waiting caller, and a leg that has just hung up
/// would otherwise take the sweep down and leave everybody else in silence.
/// </remarks>
public sealed class TelnyxQueueTreatmentProvider : IQueueTreatmentProvider
{
    private readonly TelnyxApiClient _apiClient;
    private readonly IVoiceMediaItemManager _voiceMediaItemManager;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxQueueTreatmentProvider"/> class.
    /// </summary>
    /// <param name="apiClient">The typed Telnyx client.</param>
    /// <param name="voiceMediaItemManager">The voice media catalog, used to resolve a clip to its provider name.</param>
    /// <param name="logger">The logger.</param>
    public TelnyxQueueTreatmentProvider(
        TelnyxApiClient apiClient,
        IVoiceMediaItemManager voiceMediaItemManager,
        ILogger<TelnyxQueueTreatmentProvider> logger)
    {
        _apiClient = apiClient;
        _voiceMediaItemManager = voiceMediaItemManager;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task SpeakAsync(string providerCallId, string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerCallId) || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var result = await _apiClient.SpeakAsync(providerCallId, text, cancellationToken: cancellationToken);

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
    public async Task OfferChoiceAsync(string providerCallId, string text, string acceptKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerCallId) || string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(acceptKey))
        {
            return;
        }

        var result = await _apiClient.GatherAsync(providerCallId, text, acceptKey, cancellationToken: cancellationToken);

        Report(result.Succeeded, "gather_using_speak", providerCallId);
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
