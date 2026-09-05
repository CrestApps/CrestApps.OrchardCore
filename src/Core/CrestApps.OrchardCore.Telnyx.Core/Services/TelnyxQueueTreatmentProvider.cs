using CrestApps.Core.Support;
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
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxQueueTreatmentProvider"/> class.
    /// </summary>
    /// <param name="apiClient">The typed Telnyx client.</param>
    /// <param name="logger">The logger.</param>
    public TelnyxQueueTreatmentProvider(TelnyxApiClient apiClient, ILogger<TelnyxQueueTreatmentProvider> logger)
    {
        _apiClient = apiClient;
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

        // Looped: music that plays once leaves the caller in silence for the rest of their wait, which sounds
        // exactly like a call that has dropped.
        var result = await _apiClient.PlaybackAsync(providerCallId, mediaId, loop: true, cancellationToken);

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
