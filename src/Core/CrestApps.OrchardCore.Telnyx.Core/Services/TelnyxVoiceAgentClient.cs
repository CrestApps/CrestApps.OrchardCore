using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CrestApps.Core.Support;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Default <see cref="ITelnyxVoiceAgentClient"/> implementation over the Telnyx Call Control v2 REST API.
/// </summary>
public sealed class TelnyxVoiceAgentClient : ITelnyxVoiceAgentClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TelnyxOptions _options;
    private readonly ILogger _logger;

    public TelnyxVoiceAgentClient(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<TelnyxOptions> options,
        ILogger<TelnyxVoiceAgentClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.CurrentValue;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<string> OriginateAsync(string to, string from, TelnyxOutboundBridgeState clientState, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(to);
        ArgumentNullException.ThrowIfNull(clientState);

        var body = new Dictionary<string, object>
        {
            ["connection_id"] = _options.ConnectionId,
            ["to"] = to,
            ["client_state"] = clientState.ToClientState(),
            ["command_id"] = $"ai-voice-dial-{clientState.ActivityId}",
        };

        var caller = string.IsNullOrWhiteSpace(from) ? _options.DefaultOutboundCallerId : from;

        if (!string.IsNullOrWhiteSpace(caller))
        {
            body["from"] = caller;
        }

        // A PSTN destination is terminated through the outbound voice profile so it routes as an outbound call.
        if (!string.IsNullOrWhiteSpace(_options.OutboundVoiceProfileId))
        {
            body["outbound_voice_profile_id"] = _options.OutboundVoiceProfileId;
        }

        var response = await PostAsync("calls", body, cancellationToken);

        if (response is null || !response.IsSuccessStatusCode)
        {
            return null;
        }

        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (document.RootElement.TryGetProperty("data", out var data) &&
                data.ValueKind == JsonValueKind.Object &&
                data.TryGetProperty("call_control_id", out var id) &&
                id.ValueKind == JsonValueKind.String)
            {
                return id.GetString();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read the call control id of an originated AI voice call.");
        }
        finally
        {
            response.Dispose();
        }

        return null;
    }

    /// <inheritdoc/>
    public Task SpeakAsync(string callControlId, string text, string voice, string language, string commandId, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object>
        {
            ["payload"] = text,
            ["payload_type"] = "text",
            ["voice"] = string.IsNullOrWhiteSpace(voice) ? "female" : voice,
            ["language"] = string.IsNullOrWhiteSpace(language) ? "en-US" : language,
        };

        if (!string.IsNullOrWhiteSpace(commandId))
        {
            body["command_id"] = commandId;
        }

        return ActionAsync(callControlId, "speak", body, cancellationToken);
    }

    /// <inheritdoc/>
    public Task StartTranscriptionAsync(string callControlId, string language, string commandId, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object>
        {
            // Engine "A" is Telnyx's Google-backed transcription; the inbound track is the far-end caller, so the
            // agent's own text-to-speech (the outbound track) is never fed back into the transcript.
            ["transcription_engine"] = "A",
            ["transcription_tracks"] = "inbound",
            ["language"] = string.IsNullOrWhiteSpace(language) ? "en" : language,
        };

        if (!string.IsNullOrWhiteSpace(commandId))
        {
            body["command_id"] = commandId;
        }

        return ActionAsync(callControlId, "transcription_start", body, cancellationToken);
    }

    /// <inheritdoc/>
    public Task StopTranscriptionAsync(string callControlId, CancellationToken cancellationToken = default)
        => ActionAsync(callControlId, "transcription_stop", body: null, cancellationToken);

    /// <inheritdoc/>
    public Task HangupAsync(string callControlId, CancellationToken cancellationToken = default)
        => ActionAsync(callControlId, "hangup", body: null, cancellationToken);

    private async Task ActionAsync(string callControlId, string action, Dictionary<string, object> body, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(callControlId))
        {
            return;
        }

        var response = await PostAsync($"calls/{Uri.EscapeDataString(callControlId)}/actions/{action}", body, cancellationToken);

        if (response is null)
        {
            return;
        }

        try
        {
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Telnyx rejected the AI voice '{Action}' command with status code {StatusCode}. Response: {Response}",
                    action.SanitizeLogValue(),
                    response.StatusCode,
                    (await SafeReadAsync(response, cancellationToken)).SanitizeLogValue());
            }
        }
        finally
        {
            response.Dispose();
        }
    }

    private async Task<HttpResponseMessage> PostAsync(string path, Dictionary<string, object> body, CancellationToken cancellationToken)
    {
        try
        {
            var client = CreateClient();
            using var content = JsonContent.Create(body ?? [], options: TelnyxJsonSerializerOptions.Default);

            return await client.PostAsync(path, content, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while calling the Telnyx Call Control endpoint '{Path}'.", path.SanitizeLogValue());

            return null;
        }
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient(TelnyxConstants.ProviderTechnicalName);
        client.BaseAddress = new Uri(_options.ApiBaseUrl);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        return client;
    }

    private static async Task<string> SafeReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch
        {
            return string.Empty;
        }
    }
}
