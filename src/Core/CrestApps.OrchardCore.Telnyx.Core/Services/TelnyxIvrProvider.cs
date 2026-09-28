using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Plays a phone menu to a caller on a Telnyx leg and collects the key they press. The key arrives back as a
/// <c>call.gather.ended</c> webhook, which the Contact Center inbound path applies to the flow.
/// </summary>
/// <remarks>
/// A menu is played with <c>gather_using_speak</c> (text) or <c>gather_using_audio</c> (a recorded clip). Both need
/// the call answered first, and both replay the prompt up to <c>maximum_tries</c> times on their own when nobody
/// presses anything; this collects once (<c>maximum_tries</c> 1) and lets the flow decide what a missed key means,
/// so a caller is not played the same menu three times by Telnyx and then three more by the flow.
/// </remarks>
public sealed class TelnyxIvrProvider : IIvrProvider
{
    private readonly TelnyxApiClient _apiClient;
    private readonly IVoiceMediaItemManager _voiceMediaItemManager;
    private readonly IOptionsMonitor<TelnyxOptions> _options;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxIvrProvider"/> class.
    /// </summary>
    /// <param name="apiClient">The typed Telnyx client.</param>
    /// <param name="voiceMediaItemManager">The voice media catalog, used to resolve a recorded menu to its Telnyx name.</param>
    /// <param name="options">The tenant's Telnyx options, for the voice and language menus are spoken in.</param>
    /// <param name="logger">The logger.</param>
    public TelnyxIvrProvider(
        TelnyxApiClient apiClient,
        IVoiceMediaItemManager voiceMediaItemManager,
        IOptionsMonitor<TelnyxOptions> options,
        ILogger<TelnyxIvrProvider> logger)
    {
        _apiClient = apiClient;
        _voiceMediaItemManager = voiceMediaItemManager;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> AnswerAsync(string providerCallId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerCallId))
        {
            return false;
        }

        var result = await _apiClient.AnswerAsync(providerCallId, cancellationToken: cancellationToken);

        return result.Succeeded;
    }

    /// <inheritdoc/>
    public async Task<bool> PromptAsync(
        string providerCallId,
        string text,
        string mediaId,
        string validDigits,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerCallId))
        {
            return false;
        }

        var body = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            // One key per menu level. A menu that waits for several digits leaves the caller wondering whether it
            // heard them, and every option here is a single key.
            ["minimum_digits"] = 1,
            ["maximum_digits"] = 1,
            ["maximum_tries"] = 1,
            ["timeout_millis"] = TelnyxConstants.Gather.TimeoutMillis,
        };

        if (!string.IsNullOrWhiteSpace(validDigits))
        {
            body["valid_digits"] = validDigits;
            body["terminating_digit"] = TelnyxPrompts.PickTerminatingDigit(validDigits);
        }

        // Recorded audio wins over synthesized speech when the menu has it: a tenant who recorded their menu did
        // so because they did not want it read out.
        if (!string.IsNullOrWhiteSpace(mediaId))
        {
            var audio = await ResolveAudioAsync(mediaId, cancellationToken);

            if (audio.HasValue)
            {
                body[audio.Value.Field] = audio.Value.Value;

                var played = await _apiClient.PostCallActionAsync(providerCallId, "gather_using_audio", body, cancellationToken);

                return played.Succeeded;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning(
                    "The recorded menu '{MediaId}' could not be resolved and the menu has no text to speak instead, so nothing was played on call '{CallId}'.",
                    mediaId.SanitizeLogValue(),
                    providerCallId.SanitizeLogValue());

                return false;
            }

            _logger.LogWarning(
                "The recorded menu '{MediaId}' could not be resolved, so the menu's text is spoken instead on call '{CallId}'.",
                mediaId.SanitizeLogValue(),
                providerCallId.SanitizeLogValue());
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        // Telnyx requires a voice for speech; without one the command is refused and the caller hears nothing. The
        // voice and language are the tenant's, the same ones the voicemail greeting and a queue's callback offer use.
        body["payload"] = text;
        body["payload_type"] = "text";
        TelnyxPrompts.ApplySpeech(body, _options.CurrentValue);

        var result = await _apiClient.PostCallActionAsync(providerCallId, "gather_using_speak", body, cancellationToken);

        return result.Succeeded;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Spoken with <c>speak</c> in the same voice and language as the menu. The command carries a call-flow
    /// <c>client_state</c>, which Telnyx echoes on the message's <c>call.speak.ended</c>: that webhook is what moves the
    /// caller on, so the menu, the hold music or the voicemail greeting is only started once the whole message has been
    /// heard, rather than being issued straight after it and cutting it off. A message after which the call is ended
    /// carries the hang-up state instead, and the same webhook hangs up.
    /// </remarks>
    public async Task<bool> AnnounceAsync(
        string providerCallId,
        string text,
        bool endCallAfter,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerCallId) || string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var options = _options.CurrentValue;
        var clientState = endCallAfter
            ? TelnyxCallFlowClientState.ForHangUpAfterSpeech()
            : TelnyxCallFlowClientState.ForAnnouncement();

        var result = await _apiClient.SpeakAsync(
            providerCallId,
            text,
            TelnyxPrompts.ResolveVoice(options),
            TelnyxPrompts.ResolveLanguage(options),
            clientState.ToJson(),
            cancellationToken);

        if (!result.Succeeded)
        {
            _logger.LogWarning(
                "Telnyx refused to speak the entry point's message on call '{CallId}' (status {StatusCode}): {Error}",
                providerCallId.SanitizeLogValue(),
                result.StatusCode,
                result.ErrorBody.SanitizeLogValue());
        }

        return result.Succeeded;
    }

    /// <summary>
    /// Turns the menu's configured media into what Telnyx will play: the <c>media_name</c> a voice media catalog clip
    /// was stored under, or an <c>audio_url</c> for externally hosted audio.
    /// </summary>
    /// <remarks>
    /// The menu stores a catalog identifier, which means nothing to Telnyx; sending it as a URL was refused and the
    /// caller heard silence. This is the same resolution queue hold music uses.
    /// </remarks>
    private async Task<(string Field, string Value)?> ResolveAudioAsync(string mediaId, CancellationToken cancellationToken)
    {
        if (Uri.TryCreate(mediaId, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return ("audio_url", mediaId);
        }

        var item = await _voiceMediaItemManager.FindByIdAsync(mediaId, cancellationToken);

        if (item is null || string.IsNullOrWhiteSpace(item.MediaReference))
        {
            return null;
        }

        return ("media_name", item.MediaReference);
    }
}
