using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// The one place that talks to the Telnyx API.
/// <para>
/// Four services each built their own <see cref="HttpClient"/>, set their own base address and bearer header, and
/// read the provider's answer their own way — four places to fix a bug, four chances to disagree about what a 429
/// means, and no retry anywhere, so a rate-limited command was a call that simply did not happen.
/// </para>
/// <para>
/// Commands return a result rather than throwing, because every caller is handling a live call: an exception on a
/// refused command abandons a customer mid-flow, while a result lets the caller fail deliberately and say why.
/// </para>
/// </summary>
public sealed class TelnyxApiClient
{
    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly TelnyxOptions _options;
    private readonly TelnyxApiRetryPolicy _retryPolicy;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxApiClient"/> class.
    /// </summary>
    public TelnyxApiClient(
        HttpClient httpClient,
        IOptions<TelnyxOptions> options,
        TelnyxApiRetryPolicy retryPolicy,
        ILogger<TelnyxApiClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _retryPolicy = retryPolicy;
        _logger = logger;
    }

    /// <summary>
    /// Answers a ringing call. Answering an already-answered call is harmless, so this is retried.
    /// </summary>
    /// <param name="callControlId">The provider's identifier for the leg.</param>
    /// <param name="clientState">The opaque state echoed back on every event for the leg.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public Task<TelnyxApiResult> AnswerAsync(string callControlId, string clientState = null, CancellationToken cancellationToken = default)
        => PostActionAsync(callControlId, "answer", BuildClientStateBody(clientState), retryable: true, cancellationToken);

    /// <summary>
    /// Hangs a call up. Hanging up a call that has already ended is harmless, so this is retried.
    /// </summary>
    /// <param name="callControlId">The provider's identifier for the leg.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public Task<TelnyxApiResult> HangupAsync(string callControlId, CancellationToken cancellationToken = default)
        => PostActionAsync(callControlId, "hangup", new Dictionary<string, object>(), retryable: true, cancellationToken);

    /// <summary>
    /// Places a call.
    /// </summary>
    /// <remarks>
    /// Never retried. Placing a call is not idempotent, and a retry the provider had in fact accepted would dial
    /// the customer twice, which is a worse outcome than the command failing.
    /// </remarks>
    /// <param name="request">What to dial, and as whom.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task<TelnyxCallApiResult> OriginateAsync(TelnyxOriginateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var body = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["to"] = request.To,
            ["from"] = request.From,
            ["connection_id"] = request.ConnectionId,
        };

        if (!string.IsNullOrWhiteSpace(request.SipAuthUsername))
        {
            body["sip_auth_username"] = request.SipAuthUsername;
        }

        if (!string.IsNullOrWhiteSpace(request.ClientState))
        {
            body["client_state"] = EncodeClientState(request.ClientState);
        }

        if (request.TimeoutSeconds is > 0)
        {
            body["timeout_secs"] = request.TimeoutSeconds.Value;
        }

        foreach (var field in request.AdditionalFields)
        {
            body[field.Key] = field.Value;
        }

        var (result, json) = await SendAsync(HttpMethod.Post, "calls", body, retryable: false, cancellationToken);

        return new TelnyxCallApiResult
        {
            Succeeded = result.Succeeded,
            StatusCode = result.StatusCode,
            ErrorBody = result.ErrorBody,
            CallControlId = ReadDataString(json, "call_control_id"),
        };
    }

    /// <summary>
    /// Bridges two legs together.
    /// </summary>
    /// <param name="callControlId">The leg the bridge is issued against.</param>
    /// <param name="otherCallControlId">The leg to bridge it to.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public Task<TelnyxApiResult> BridgeAsync(string callControlId, string otherCallControlId, CancellationToken cancellationToken = default)
        => PostActionAsync(
            callControlId,
            "bridge",
            new Dictionary<string, object>(StringComparer.Ordinal) { ["call_control_id"] = otherCallControlId },
            retryable: true,
            cancellationToken);

    /// <summary>
    /// Transfers a call to another destination.
    /// </summary>
    /// <remarks>
    /// Never retried, for the same reason as originating: a transfer the provider accepted and then failed to
    /// acknowledge would, on retry, move a customer twice.
    /// </remarks>
    /// <param name="callControlId">The leg to transfer.</param>
    /// <param name="to">The destination.</param>
    /// <param name="from">The caller identity to present, when it differs from the leg's.</param>
    /// <param name="clientState">The opaque state echoed back on every event for the leg.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public Task<TelnyxApiResult> TransferAsync(
        string callControlId,
        string to,
        string from = null,
        string clientState = null,
        CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object>(StringComparer.Ordinal) { ["to"] = to };

        if (!string.IsNullOrWhiteSpace(from))
        {
            body["from"] = from;
        }

        if (!string.IsNullOrWhiteSpace(clientState))
        {
            body["client_state"] = EncodeClientState(clientState);
        }

        return PostActionAsync(callControlId, "transfer", body, retryable: false, cancellationToken);
    }

    /// <summary>
    /// Speaks a message to the caller.
    /// </summary>
    /// <param name="callControlId">The leg to speak on.</param>
    /// <param name="text">What to say.</param>
    /// <param name="voice">The voice to say it in.</param>
    /// <param name="language">The language to say it in.</param>
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

        if (!string.IsNullOrWhiteSpace(voice))
        {
            body["voice"] = voice;
        }

        if (!string.IsNullOrWhiteSpace(language))
        {
            body["language"] = language;
        }

        if (!string.IsNullOrWhiteSpace(clientState))
        {
            body["client_state"] = EncodeClientState(clientState);
        }

        // Not retried: a retry that the provider had accepted would say the same thing to the caller twice.
        return PostActionAsync(callControlId, "speak", body, retryable: false, cancellationToken);
    }

    /// <summary>
    /// Starts playing audio to the caller, such as hold music.
    /// </summary>
    /// <param name="callControlId">The leg to play on.</param>
    /// <param name="audioUrl">The audio to play.</param>
    /// <param name="loop">Whether to loop until stopped.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public Task<TelnyxApiResult> PlaybackAsync(
        string callControlId,
        string audioUrl,
        bool loop = false,
        CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object>(StringComparer.Ordinal) { ["audio_url"] = audioUrl };

        if (loop)
        {
            body["loop"] = "infinity";
        }

        return PostActionAsync(callControlId, "playback_start", body, retryable: false, cancellationToken);
    }

    /// <summary>
    /// Speaks a prompt and collects a key press.
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
        };

        if (!string.IsNullOrWhiteSpace(clientState))
        {
            body["client_state"] = EncodeClientState(clientState);
        }

        return PostActionAsync(callControlId, "gather_using_speak", body, retryable: false, cancellationToken);
    }

    /// <summary>
    /// Starts recording a call.
    /// </summary>
    /// <param name="callControlId">The leg to record.</param>
    /// <param name="channels">Whether to record a single mixed channel or both separately.</param>
    /// <param name="clientState">The opaque state echoed back on every event for the leg.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public Task<TelnyxApiResult> RecordStartAsync(
        string callControlId,
        string channels = "dual",
        string clientState = null,
        CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["format"] = "mp3",
            ["channels"] = channels,
        };

        if (!string.IsNullOrWhiteSpace(clientState))
        {
            body["client_state"] = EncodeClientState(clientState);
        }

        return PostActionAsync(callControlId, "record_start", body, retryable: false, cancellationToken);
    }

    /// <summary>
    /// Reads a call's current provider-side state.
    /// </summary>
    /// <param name="callControlId">The leg to read.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task<(TelnyxApiResult Result, JsonElement? Data)> GetCallAsync(string callControlId, CancellationToken cancellationToken = default)
    {
        var (result, json) = await SendAsync(
            HttpMethod.Get,
            $"calls/{Uri.EscapeDataString(callControlId ?? string.Empty)}",
            body: null,
            retryable: true,
            cancellationToken);

        return (result, json);
    }

    /// <summary>
    /// Lists the calls the provider currently has up on a connection.
    /// </summary>
    /// <remarks>
    /// This is the only way to see a call the platform lost track of - one placed just before the process died,
    /// so no interaction was ever written for it. Reconciliation from local records can never find those,
    /// because there is no local record to start from.
    /// </remarks>
    /// <param name="connectionId">The Telnyx connection whose calls to list.</param>
    /// <param name="pageToken">The cursor from a previous page, when continuing a listing.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task<TelnyxActiveCallListResult> ListActiveCallsAsync(
        string connectionId,
        string pageToken = null,
        CancellationToken cancellationToken = default)
    {
        var path = $"connections/{Uri.EscapeDataString(connectionId ?? string.Empty)}/active_calls";

        if (!string.IsNullOrWhiteSpace(pageToken))
        {
            path += $"?page[after]={Uri.EscapeDataString(pageToken)}";
        }

        var (result, json) = await SendAsync(HttpMethod.Get, path, body: null, retryable: true, cancellationToken);

        var calls = new List<TelnyxActiveCall>();
        string nextPageToken = null;

        if (json is JsonElement root && root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in data.EnumerateArray())
                {
                    if (element.ValueKind != JsonValueKind.Object ||
                        !element.TryGetProperty("call_control_id", out var id) ||
                        id.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    calls.Add(new TelnyxActiveCall
                    {
                        CallControlId = id.GetString(),
                        ClientState = element.TryGetProperty("client_state", out var state) && state.ValueKind == JsonValueKind.String
                            ? state.GetString()
                            : null,
                        DurationSeconds = element.TryGetProperty("call_duration", out var duration) && duration.ValueKind == JsonValueKind.Number
                            ? duration.GetInt32()
                            : 0,
                    });
                }
            }

            if (root.TryGetProperty("meta", out var meta) && meta.ValueKind == JsonValueKind.Object &&
                meta.TryGetProperty("next_page_token", out var next) && next.ValueKind == JsonValueKind.String)
            {
                nextPageToken = next.GetString();
            }
        }

        return new TelnyxActiveCallListResult
        {
            Succeeded = result.Succeeded,
            StatusCode = result.StatusCode,
            ErrorBody = result.ErrorBody,
            Calls = calls,
            NextPageToken = nextPageToken,
        };
    }

    /// <summary>
    /// Creates a conference, or reports the conflict when one of that name already exists.
    /// </summary>
    /// <param name="name">The deterministic conference name.</param>
    /// <param name="callControlId">The leg the conference is created around.</param>
    /// <param name="commandId">The caller's idempotency key, which Telnyx uses to collapse a repeated command.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task<TelnyxConferenceApiResult> CreateConferenceAsync(
        string name,
        string callControlId,
        string commandId = null,
        CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["name"] = name,
            ["call_control_id"] = callControlId,
        };

        if (!string.IsNullOrWhiteSpace(commandId))
        {
            body["command_id"] = commandId;
        }

        var (result, json) = await SendAsync(HttpMethod.Post, "conferences", body, retryable: false, cancellationToken);

        return new TelnyxConferenceApiResult
        {
            Succeeded = result.Succeeded,
            StatusCode = result.StatusCode,
            ErrorBody = result.ErrorBody,
            ConferenceId = ReadDataString(json, "id"),
        };
    }

    /// <summary>
    /// Finds an existing conference by its name.
    /// </summary>
    /// <param name="name">The deterministic conference name.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task<TelnyxConferenceApiResult> FindConferenceByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var (result, json) = await SendAsync(
            HttpMethod.Get,
            $"conferences?filter[name]={Uri.EscapeDataString(name ?? string.Empty)}",
            body: null,
            retryable: true,
            cancellationToken);

        return new TelnyxConferenceApiResult
        {
            Succeeded = result.Succeeded,
            StatusCode = result.StatusCode,
            ErrorBody = result.ErrorBody,
            ConferenceId = ReadFirstArrayItemString(json, "id"),
        };
    }

    /// <summary>
    /// Joins a leg to a conference.
    /// </summary>
    /// <param name="conferenceId">The conference.</param>
    /// <param name="callControlId">The leg to join.</param>
    /// <param name="endConferenceOnExit">Whether the conference ends when this leg leaves.</param>
    /// <param name="commandId">The caller's idempotency key.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task<TelnyxApiResult> JoinConferenceAsync(
        string conferenceId,
        string callControlId,
        bool endConferenceOnExit = false,
        string commandId = null,
        CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["call_control_id"] = callControlId,
        };

        if (endConferenceOnExit)
        {
            body["end_conference_on_exit"] = true;
        }

        if (!string.IsNullOrWhiteSpace(commandId))
        {
            body["command_id"] = commandId;
        }

        var (result, _) = await SendAsync(
            HttpMethod.Post,
            $"conferences/{Uri.EscapeDataString(conferenceId ?? string.Empty)}/actions/join",
            body,
            retryable: false,
            cancellationToken);

        return result;
    }

    /// <summary>
    /// Mints a browser SIP credential.
    /// </summary>
    /// <param name="connectionId">The Telnyx credential connection to mint against.</param>
    /// <param name="name">A name identifying the holder.</param>
    /// <param name="expiresUtc">When the credential should stop working, so an abandoned tab cannot hold one open forever.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task<TelnyxCredentialApiResult> CreateCredentialAsync(
        string connectionId,
        string name,
        DateTime? expiresUtc = null,
        CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["connection_id"] = connectionId,
            ["name"] = name,
        };

        if (expiresUtc is not null)
        {
            body["expires_at"] = expiresUtc.Value.ToString("O");
        }

        var (result, json) = await SendAsync(HttpMethod.Post, "telephony_credentials", body, retryable: false, cancellationToken);

        return new TelnyxCredentialApiResult
        {
            Succeeded = result.Succeeded,
            StatusCode = result.StatusCode,
            ErrorBody = result.ErrorBody,
            CredentialId = ReadDataString(json, "id"),
            SipUsername = ReadDataString(json, "sip_username"),
            SipPassword = ReadDataString(json, "sip_password"),
        };
    }

    /// <summary>
    /// Revokes a browser SIP credential.
    /// </summary>
    /// <remarks>
    /// A credential the provider has already forgotten is reported as success. Revocation runs on sign-out and on
    /// a cap eviction, both of which can race a credential that already expired provider-side; treating that 404
    /// as failure would leave a local record nobody can ever clean up.
    /// </remarks>
    /// <param name="credentialId">The credential to revoke.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task<TelnyxApiResult> DeleteCredentialAsync(string credentialId, CancellationToken cancellationToken = default)
    {
        var (result, _) = await SendAsync(
            HttpMethod.Delete,
            $"telephony_credentials/{Uri.EscapeDataString(credentialId ?? string.Empty)}",
            body: null,
            retryable: true,
            cancellationToken);

        return result.StatusCode == HttpStatusCode.NotFound
            ? TelnyxApiResult.Success(HttpStatusCode.NotFound)
            : result;
    }

    /// <summary>
    /// Posts an arbitrary call action, for the commands that have no named method here yet.
    /// </summary>
    /// <remarks>
    /// Never retried: this cannot know whether the caller's action is safe to repeat, and assuming it is would
    /// eventually repeat one that is not.
    /// </remarks>
    /// <param name="callControlId">The leg to act on.</param>
    /// <param name="action">The Telnyx action name.</param>
    /// <param name="body">The action body.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public Task<TelnyxApiResult> PostCallActionAsync(
        string callControlId,
        string action,
        IDictionary<string, object> body,
        CancellationToken cancellationToken = default)
        => PostActionAsync(callControlId, action, body ?? new Dictionary<string, object>(StringComparer.Ordinal), retryable: false, cancellationToken);

    private Task<TelnyxApiResult> PostActionAsync(
        string callControlId,
        string action,
        IDictionary<string, object> body,
        bool retryable,
        CancellationToken cancellationToken)
        => SendAsync(
                HttpMethod.Post,
                $"calls/{Uri.EscapeDataString(callControlId ?? string.Empty)}/actions/{action}",
                body,
                retryable,
                cancellationToken)
            .ContinueWith(task => task.Result.Result, cancellationToken, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

    private async Task<(TelnyxApiResult Result, JsonElement? Json)> SendAsync(
        HttpMethod method,
        string path,
        IDictionary<string, object> body,
        bool retryable,
        CancellationToken cancellationToken)
    {
        var attempts = retryable ? TelnyxApiRetryPolicy.MaxAttempts : 1;

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(method, path);

                // The bearer is attached per request rather than on the shared client, because the client is
                // registered once for the process while the key is a per-tenant setting.
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

                if (body is not null)
                {
                    request.Content = new StringContent(
                        JsonSerializer.Serialize(body, _serializerOptions),
                        Encoding.UTF8,
                        "application/json");
                }

                using var response = await _httpClient.SendAsync(request, cancellationToken);
                var content = await ReadContentAsync(response, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    return (TelnyxApiResult.Success(response.StatusCode), TryParse(content));
                }

                if (attempt < attempts && TelnyxApiRetryPolicy.IsRetryable(response.StatusCode))
                {
                    var delay = _retryPolicy.GetDelay(attempt, response.Headers.RetryAfter?.Delta);

                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug(
                            "Telnyx answered {StatusCode} for {Method} {Path}; retrying attempt {Attempt} of {Attempts} after {Delay}.",
                            (int)response.StatusCode,
                            method,
                            path,
                            attempt + 1,
                            attempts,
                            delay);
                    }

                    if (delay > TimeSpan.Zero)
                    {
                        await Task.Delay(delay, cancellationToken);
                    }

                    continue;
                }

                return (TelnyxApiResult.Failure(response.StatusCode, content), null);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (attempt < attempts)
                {
                    continue;
                }

                // A provider that cannot be reached is a failed command, not an unhandled exception on a path
                // that is holding a live call.
                _logger.LogError(ex, "The Telnyx request {Method} {Path} could not be completed.", method, path);

                return (TelnyxApiResult.Failure(null, ex.Message), null);
            }
        }
    }

    private static async Task<string> ReadContentAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // A body that cannot be read does not change whether the command succeeded.
            return string.Empty;
        }
    }

    private static JsonElement? TryParse(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(content);

            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string ReadDataString(JsonElement? json, string propertyName)
    {
        if (json is null ||
            !json.Value.TryGetProperty("data", out var data) ||
            data.ValueKind != JsonValueKind.Object ||
            !data.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return value.GetString();
    }

    private static string ReadFirstArrayItemString(JsonElement? json, string propertyName)
    {
        if (json is null ||
            !json.Value.TryGetProperty("data", out var data) ||
            data.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var item in data.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object &&
                item.TryGetProperty(propertyName, out var value) &&
                value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        return null;
    }

    private static Dictionary<string, object> BuildClientStateBody(string clientState)
    {
        var body = new Dictionary<string, object>(StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(clientState))
        {
            body["client_state"] = EncodeClientState(clientState);
        }

        return body;
    }

    private static string EncodeClientState(string clientState)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(clientState));
}
