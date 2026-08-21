using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CrestApps.Core.Support;
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
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TelnyxOptions _options;
    private readonly ILogger<TelnyxVoicemailRecordingStarter> _logger;

    public TelnyxVoicemailRecordingStarter(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<TelnyxOptions> options,
        ILogger<TelnyxVoicemailRecordingStarter> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.CurrentValue;
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
            using var client = CreateClient();
            using var content = JsonContent.Create(body, options: TelnyxJsonSerializerOptions.Default);
            using var response = await client.PostAsync(
                $"calls/{Uri.EscapeDataString(callControlId)}/actions/record_start",
                content,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var payload = await SafeReadAsync(response, cancellationToken);

                // A 404 means the leg is already gone (the caller hung up during or right after the greeting), which
                // is an expected race with nothing to record. Any other rejection is logged with the provider's
                // response so the actual reason is visible.
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    _logger.LogDebug(
                        "Voicemail record_start for call {CallControlId} was not accepted (404); the caller likely hung up before leaving a message.",
                        callControlId.SanitizeLogValue());
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

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient(TelnyxConstants.ProviderTechnicalName);
        client.BaseAddress = new Uri(_options.ApiBaseUrl);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        return client;
    }
}
