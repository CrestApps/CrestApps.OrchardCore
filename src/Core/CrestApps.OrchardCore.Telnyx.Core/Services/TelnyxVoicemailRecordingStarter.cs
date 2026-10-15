using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Default <see cref="ITelnyxVoicemailRecordingStarter"/>. It issues a Telnyx <c>record_start</c> with a leading
/// beep, tagging the recording with the voicemail correlation state so the saved-recording webhook ingests it
/// into the recipient agent's voicemail inbox.
/// </summary>
public sealed class TelnyxVoicemailRecordingStarter : ITelnyxVoicemailRecordingStarter
{
    private readonly TelnyxApiClient _apiClient;
    private readonly TelnyxOptions _options;
    private readonly ICallRecordingCatalog _callRecordingCatalog;
    private readonly ILogger<TelnyxVoicemailRecordingStarter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxVoicemailRecordingStarter"/> class.
    /// </summary>
    /// <param name="apiClient">The Telnyx Call Control client.</param>
    /// <param name="options">The tenant's Telnyx options.</param>
    /// <param name="callRecordingCatalogs">The call recordings catalog, when the Call Recording feature is enabled.</param>
    /// <param name="logger">The logger.</param>
    public TelnyxVoicemailRecordingStarter(
        TelnyxApiClient apiClient,
        IOptionsMonitor<TelnyxOptions> options,
        IEnumerable<ICallRecordingCatalog> callRecordingCatalogs,
        ILogger<TelnyxVoicemailRecordingStarter> logger)
    {
        _apiClient = apiClient;
        _options = options.CurrentValue;
        _callRecordingCatalog = callRecordingCatalogs.FirstOrDefault();
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> StartAsync(
        string callControlId,
        string interactionId,
        string recipientUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(callControlId) || !_options.IsConfigured)
        {
            return false;
        }

        var body = new Dictionary<string, object>
        {
            ["format"] = TelnyxConstants.Recording.Format,
            ["channels"] = "single",
            // Play a short beep before recording begins, so the caller hears the tone the greeting promised and no
            // part of the greeting bleeds into the message.
            ["play_beep"] = true,
        };

        if (!string.IsNullOrWhiteSpace(interactionId))
        {
            body["client_state"] = TelnyxRecordingClientState
                .ForVoicemail(interactionId, recipientUserId)
                .ToClientState();
        }

        try
        {
            await StopRunningCallRecordingAsync(callControlId, cancellationToken);

            var response = await _apiClient.PostCallActionAsync(callControlId, "record_start", body, cancellationToken);

            if (!response.Succeeded)
            {
                var payload = response.ErrorBody ?? string.Empty;

                // A 404, or a 422 "Call has already ended" (90018), means the leg is already gone: the caller hung up
                // during or right after the greeting, an expected race with nothing to record. Any other rejection is
                // logged with the provider's response so the actual reason is visible.
                if (response.StatusCode == HttpStatusCode.NotFound || TelnyxApiErrors.IsCallAlreadyEnded(response))
                {
                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug(
                            "Voicemail record_start for call {CallControlId} was not accepted ({StatusCode}); the caller hung up before leaving a message.",
                            callControlId.SanitizeLogValue(),
                            response.StatusCode);
                    }
                }
                else
                {
                    _logger.LogError(
                        "Telnyx rejected the voicemail record_start for call {CallControlId} with status code {StatusCode}. Response: {Response}",
                        callControlId.SanitizeLogValue(),
                        response.StatusCode,
                        payload.SanitizeLogValue());
                }

                return false;
            }

            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while starting the Telnyx voicemail recording for call {CallControlId}.", callControlId.SanitizeLogValue());

            return false;
        }
    }

    // A call already being recorded (an automated voice agent's call whose hand-off fell to voicemail) is stopped
    // first, so Telnyx saves the conversation as a recording of its own before the message begins. Started on top of
    // it, the voicemail's recording took the conversation's place, and the call was left without its recording.
    private async Task StopRunningCallRecordingAsync(string callControlId, CancellationToken cancellationToken)
    {
        if (_callRecordingCatalog is null ||
            await _callRecordingCatalog.FindRunningAsync(callControlId, cancellationToken) is null)
        {
            return;
        }

        var response = await _apiClient.PostCallActionAsync(callControlId, "record_stop", new Dictionary<string, object>(), cancellationToken);

        if (!response.Succeeded && _logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Telnyx did not stop the call recording on {CallControlId} before the voicemail ({StatusCode}); the voicemail is recorded anyway.",
                callControlId.SanitizeLogValue(),
                response.StatusCode);
        }
    }
}
