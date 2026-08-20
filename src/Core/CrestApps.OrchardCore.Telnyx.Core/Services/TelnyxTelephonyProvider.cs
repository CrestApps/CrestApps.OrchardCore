using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// A telephony provider that controls calls through the Telnyx Call Control (Voice API). All call control
/// happens server-side over REST; the browser soft phone carries the audio itself over the Telnyx
/// SIP-over-WebSocket registrar, so hold and mute are handled by the browser media adapter and are reported
/// optimistically here.
/// </summary>
public sealed class TelnyxTelephonyProvider :
    ITelephonyProvider,
    ITelephonyCallControlProvider,
    ITelephonyInboundCallProvider,
    ITelephonyHoldProvider,
    ITelephonyMuteProvider,
    ITelephonyTransferProvider,
    ITelephonyAttendedTransferProvider,
    ITelephonyConferenceProvider,
    ITelephonyDtmfProvider,
    ITelephonyAudioProvider,
    ITelephonySoftPhoneCredentialsProvider,
    ITelephonyCallStateProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITelnyxAgentCredentialStore _credentialStore;
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly TelnyxOptions _options;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxTelephonyProvider"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="credentialStore">The store that maps a user to their live browser SIP registration.</param>
    /// <param name="clock">The clock used to stamp call times.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    /// <param name="telnyxOptions">The active Telnyx settings resolved for the tenant shell.</param>
    public TelnyxTelephonyProvider(
        IHttpClientFactory httpClientFactory,
        ITelnyxAgentCredentialStore credentialStore,
        IClock clock,
        ILogger<TelnyxTelephonyProvider> logger,
        IStringLocalizer<TelnyxTelephonyProvider> stringLocalizer,
        IOptionsMonitor<TelnyxOptions> telnyxOptions)
    {
        _httpClientFactory = httpClientFactory;
        _credentialStore = credentialStore;
        _clock = clock;
        _logger = logger;
        _options = telnyxOptions.CurrentValue;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public LocalizedString Name => S["Telnyx"];

    /// <inheritdoc/>
    public TelephonyCapabilities Capabilities
        => TelephonyCapabilities.Dial |
            TelephonyCapabilities.Hangup |
            TelephonyCapabilities.Hold |
            TelephonyCapabilities.Resume |
            TelephonyCapabilities.Mute |
            TelephonyCapabilities.Transfer |
            TelephonyCapabilities.AttendedTransfer |
            TelephonyCapabilities.Merge |
            TelephonyCapabilities.SendDigits |
            TelephonyCapabilities.ReceiveCalls;

    /// <inheritdoc/>
    public TelephonyAudioCapabilities AudioCapabilities => TelephonyAudioCapabilities.Browser;

    /// <inheritdoc/>
    public TelephonyAudioMode ConfiguredAudioMode => TelephonyAudioMode.Browser;

    /// <inheritdoc/>
    public string BrowserMediaAdapterName => TelnyxConstants.BrowserMediaAdapterName;

    /// <inheritdoc/>
    public Task<TelephonyClientCredentials> GetClientCredentialsAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
        {
            return Task.FromResult<TelephonyClientCredentials>(null);
        }

        // The browser logs in to Telnyx's WebRTC gateway with a short-lived credential minted by the
        // soft-phone registration endpoint, so the client credentials only need to declare that Telnyx
        // delivers browser audio through the Telnyx WebRTC SDK adapter. The audio fields are set explicitly
        // because the registration endpoint reads the provider's raw credentials without the enrichment the
        // SignalR credential path applies.
        return Task.FromResult(new TelephonyClientCredentials
        {
            ProviderName = TelnyxConstants.ProviderTechnicalName,
            AudioCapabilities = TelephonyAudioCapabilities.Browser,
            AudioMode = TelephonyAudioMode.Browser,
            BrowserMediaAdapterName = TelnyxConstants.BrowserMediaAdapterName,
            Settings = new Dictionary<string, string>(),
        });
    }

    /// <inheritdoc/>
    public async Task<TelephonyResult> DialAsync(DialRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.To))
        {
            return TelephonyResult.Failed(S["A destination phone number is required to place a call."].Value);
        }

        if (!_options.IsConfigured)
        {
            return NotConfigured();
        }

        var callerId = string.IsNullOrWhiteSpace(request.From) ? _options.DefaultOutboundCallerId : request.From;

        // When the caller has a live browser soft-phone registration, ring their browser first and let the
        // webhook orchestration dial the destination and bridge the two legs, so the agent hears the call in
        // the browser. Without a registration (for example an external-device provider), fall back to placing
        // the destination leg directly.
        var agentEndpoint = await ResolveBrowserAgentEndpointAsync(request, cancellationToken);

        if (agentEndpoint is not null)
        {
            return await DialBrowserBridgeAsync(request, callerId, agentEndpoint, cancellationToken);
        }

        var body = new Dictionary<string, object>
        {
            ["connection_id"] = _options.ConnectionId,
            ["to"] = request.To,
        };

        if (!string.IsNullOrWhiteSpace(callerId))
        {
            body["from"] = callerId;
        }

        if (!string.IsNullOrWhiteSpace(_options.OutboundVoiceProfileId))
        {
            body["outbound_voice_profile_id"] = _options.OutboundVoiceProfileId;
        }

        // Telnyx de-duplicates a repeated command by its command_id, so an idempotency key supplied by the
        // caller becomes the command id: a retried outbound POST after a lost response is then rejected as a
        // duplicate instead of placing a second call.
        if (request.Metadata?.TryGetValue(
            TelephonyConstants.RequestMetadata.IdempotencyKey,
            out var idempotencyKey) == true &&
            !string.IsNullOrWhiteSpace(idempotencyKey))
        {
            body["command_id"] = idempotencyKey;
        }

        try
        {
            using var client = CreateClient();
            using var content = JsonContent.Create(body, options: TelnyxJsonSerializerOptions.Default);
            using var response = await client.PostAsync("calls", content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var payload = await SafeReadContentAsync(response, cancellationToken);

                _logger.LogError(
                    "Telnyx rejected a dial request with status code {StatusCode}. Response: {Response}",
                    response.StatusCode,
                    payload.SanitizeLogValue());

                if (TelephonyProviderResponse.IsAmbiguousStatusCode(response.StatusCode))
                {
                    return TelephonyResult.Unknown(S["Telnyx did not confirm whether the call was placed."].Value);
                }

                return TelephonyResult.Failed(S["Telnyx could not place the call."].Value);
            }

            var callControlId = await ReadDataStringAsync(response, "call_control_id", cancellationToken);

            var call = new TelephonyCall
            {
                CallId = callControlId,
                From = callerId,
                To = request.To,
                State = CallState.Connecting,
                Direction = CallDirection.Outbound,
                ProviderName = TelnyxConstants.ProviderTechnicalName,
                StartedUtc = _clock.UtcNow,
            };

            return TelephonyResult.Success(call);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            _logger.LogError(ex, "An error occurred while placing a Telnyx call.");

            return TelephonyResult.Unknown(S["Telnyx did not confirm whether the call was placed."].Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while preparing a Telnyx call.");

            return TelephonyResult.Failed(S["Telnyx could not place the call."].Value);
        }
    }

    private async Task<string> ResolveBrowserAgentEndpointAsync(DialRequest request, CancellationToken cancellationToken)
    {
        if (request.Metadata is null ||
            !request.Metadata.TryGetValue(TelephonyConstants.RequestMetadata.SoftPhoneUserId, out var userId) ||
            string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var live = await _credentialStore.ListLiveByUserAsync(userId.Trim(), _clock.UtcNow, cancellationToken);
        var credential = live.Count > 0 ? live[0] : null;

        if (credential is null || string.IsNullOrWhiteSpace(credential.SipUsername))
        {
            return null;
        }

        var sipDomain = string.IsNullOrWhiteSpace(_options.SipDomain) ? TelnyxConstants.DefaultSipDomain : _options.SipDomain;

        return $"sip:{credential.SipUsername}@{sipDomain}";
    }

    private async Task<TelephonyResult> DialBrowserBridgeAsync(DialRequest request, string callerId, string agentEndpoint, CancellationToken cancellationToken)
    {
        // Ring the agent's browser endpoint. The destination and caller id travel in client_state so the
        // webhook orchestration can dial the destination once the browser answers and then bridge the legs.
        var body = new Dictionary<string, object>
        {
            ["connection_id"] = _options.ConnectionId,
            ["to"] = agentEndpoint,
            ["client_state"] = new TelnyxOutboundBridgeState
            {
                Intent = TelnyxOutboundBridgeState.AgentLegIntent,
                Destination = request.To,
                CallerId = callerId,
            }.ToClientState(),
        };

        if (!string.IsNullOrWhiteSpace(callerId))
        {
            body["from"] = callerId;
        }

        if (request.Metadata?.TryGetValue(
            TelephonyConstants.RequestMetadata.IdempotencyKey,
            out var idempotencyKey) == true &&
            !string.IsNullOrWhiteSpace(idempotencyKey))
        {
            body["command_id"] = idempotencyKey;
        }

        try
        {
            using var client = CreateClient();
            using var content = JsonContent.Create(body, options: TelnyxJsonSerializerOptions.Default);
            using var response = await client.PostAsync("calls", content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var payload = await SafeReadContentAsync(response, cancellationToken);

                _logger.LogError(
                    "Telnyx rejected an outbound-bridge agent leg with status code {StatusCode}. Response: {Response}",
                    response.StatusCode,
                    payload.SanitizeLogValue());

                if (TelephonyProviderResponse.IsAmbiguousStatusCode(response.StatusCode))
                {
                    return TelephonyResult.Unknown(S["Telnyx did not confirm whether the call was placed."].Value);
                }

                return TelephonyResult.Failed(S["Telnyx could not place the call."].Value);
            }

            var callControlId = await ReadDataStringAsync(response, "call_control_id", cancellationToken);

            // The soft phone tracks the agent leg: hanging it up ends the call, and Telnyx tears down the
            // bridged destination leg with it. The dialed number is shown as the destination, not the SIP uri.
            var call = new TelephonyCall
            {
                CallId = callControlId,
                From = callerId,
                To = request.To,
                State = CallState.Connecting,
                Direction = CallDirection.Outbound,
                ProviderName = TelnyxConstants.ProviderTechnicalName,
                StartedUtc = _clock.UtcNow,
            };

            return TelephonyResult.Success(call);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            _logger.LogError(ex, "An error occurred while placing a Telnyx outbound-bridge call.");

            return TelephonyResult.Unknown(S["Telnyx did not confirm whether the call was placed."].Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while preparing a Telnyx outbound-bridge call.");

            return TelephonyResult.Failed(S["Telnyx could not place the call."].Value);
        }
    }

    /// <inheritdoc/>
    public Task<TelephonyResult> HangupAsync(CallReference call, CancellationToken cancellationToken = default)
        => ExecuteActionAsync(call?.CallId, "hangup", body: null, () => BuildCall(call?.CallId, CallState.Disconnected, call?.Metadata), cancellationToken, succeedWhenMissing: true);

    /// <inheritdoc/>
    public Task<TelephonyResult> AnswerAsync(CallReference call, CancellationToken cancellationToken = default)
        => ExecuteActionAsync(call?.CallId, "answer", body: null, () => BuildCall(call?.CallId, CallState.Connected, call?.Metadata, CallDirection.Inbound), cancellationToken);

    /// <inheritdoc/>
    public Task<TelephonyResult> RejectAsync(CallReference call, CancellationToken cancellationToken = default)
        => ExecuteActionAsync(
            call?.CallId,
            "reject",
            new Dictionary<string, object> { ["cause"] = "CALL_REJECTED" },
            () => BuildCall(call?.CallId, CallState.Disconnected, call?.Metadata, CallDirection.Inbound),
            cancellationToken,
            succeedWhenMissing: true);

    // Hold, resume, mute, and unmute are executed by the browser media adapter (SIP re-INVITE and local track
    // toggling) because Telnyx delivers this call's audio to the browser, not to a server-side leg. The result
    // reports the target state so the soft phone drives the adapter; no Telnyx REST call is required.
    /// <inheritdoc/>
    public Task<TelephonyResult> HoldAsync(CallReference call, CancellationToken cancellationToken = default)
        => Task.FromResult(RequireCallId(call?.CallId, S["A call id is required to hold the call."].Value)
            ?? TelephonyResult.Success(BuildCall(call?.CallId, CallState.OnHold, call?.Metadata, isOnHold: true)));

    /// <inheritdoc/>
    public Task<TelephonyResult> ResumeAsync(CallReference call, CancellationToken cancellationToken = default)
        => Task.FromResult(RequireCallId(call?.CallId, S["A call id is required to resume the call."].Value)
            ?? TelephonyResult.Success(BuildCall(call?.CallId, CallState.Connected, call?.Metadata)));

    /// <inheritdoc/>
    public Task<TelephonyResult> MuteAsync(CallReference call, CancellationToken cancellationToken = default)
        => Task.FromResult(RequireCallId(call?.CallId, S["A call id is required to mute the call."].Value)
            ?? TelephonyResult.Success(BuildCall(call?.CallId, CallState.Connected, call?.Metadata, isMuted: true)));

    /// <inheritdoc/>
    public Task<TelephonyResult> UnmuteAsync(CallReference call, CancellationToken cancellationToken = default)
        => Task.FromResult(RequireCallId(call?.CallId, S["A call id is required to unmute the call."].Value)
            ?? TelephonyResult.Success(BuildCall(call?.CallId, CallState.Connected, call?.Metadata)));

    /// <inheritdoc/>
    public Task<TelephonyResult> SendDigitsAsync(SendDigitsRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Digits))
        {
            return Task.FromResult(TelephonyResult.Failed(S["Digits are required."].Value));
        }

        return ExecuteActionAsync(
            request.CallId,
            "send_dtmf",
            new Dictionary<string, object> { ["digits"] = request.Digits },
            () => null,
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task<TelephonyResult> StartAttendedTransferAsync(TransferRequest request, CancellationToken cancellationToken = default)
        => TransferCoreAsync(request, cancellationToken);

    /// <inheritdoc/>
    public Task<TelephonyResult> TransferAsync(TransferRequest request, CancellationToken cancellationToken = default)
        => TransferCoreAsync(request, cancellationToken);

    private Task<TelephonyResult> TransferCoreAsync(TransferRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.To))
        {
            return Task.FromResult(TelephonyResult.Failed(S["A destination is required to transfer a call."].Value));
        }

        var body = new Dictionary<string, object> { ["to"] = request.To };

        if (!string.IsNullOrWhiteSpace(_options.DefaultOutboundCallerId))
        {
            body["from"] = _options.DefaultOutboundCallerId;
        }

        var state = request.Mode == TransferMode.Warm ? CallState.Connected : CallState.Disconnected;

        return ExecuteActionAsync(
            request.CallId,
            "transfer",
            body,
            () => BuildCall(request.CallId, state, metadata: null),
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<TelephonyResult> MergeAsync(MergeRequest request, CancellationToken cancellationToken = default)
    {
        var callIds = request?.GetCallIds();

        if (callIds is null || callIds.Count < 2)
        {
            return TelephonyResult.Failed(S["At least two calls are required to merge calls."].Value);
        }

        if (!_options.IsConfigured)
        {
            return NotConfigured();
        }

        var primaryCallId = callIds[0];
        var conferenceName = string.IsNullOrWhiteSpace(request.ConferenceName)
            ? $"conf-{primaryCallId}"
            : request.ConferenceName;

        try
        {
            using var client = CreateClient();

            // Create the conference from the primary call, then join the remaining calls into it.
            using var createResponse = await client.PostAsync(
                "conferences",
                JsonContent.Create(
                    new Dictionary<string, object>
                    {
                        ["name"] = conferenceName,
                        ["call_control_id"] = primaryCallId,
                    },
                    options: TelnyxJsonSerializerOptions.Default),
                cancellationToken);

            if (!createResponse.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Telnyx rejected a conference creation request with status code {StatusCode}. Response: {Response}",
                    createResponse.StatusCode,
                    (await SafeReadContentAsync(createResponse, cancellationToken)).SanitizeLogValue());

                return TelephonyResult.Failed(S["Telnyx could not merge the calls."].Value);
            }

            var conferenceId = await ReadDataStringAsync(createResponse, "id", cancellationToken);

            if (string.IsNullOrWhiteSpace(conferenceId))
            {
                return TelephonyResult.Failed(S["Telnyx could not merge the calls."].Value);
            }

            foreach (var secondaryCallId in callIds.Skip(1))
            {
                using var joinResponse = await client.PostAsync(
                    $"conferences/{Uri.EscapeDataString(conferenceId)}/actions/join",
                    JsonContent.Create(
                        new Dictionary<string, object> { ["call_control_id"] = secondaryCallId },
                        options: TelnyxJsonSerializerOptions.Default),
                    cancellationToken);

                if (!joinResponse.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "Telnyx rejected a conference join request with status code {StatusCode}. Response: {Response}",
                        joinResponse.StatusCode,
                        (await SafeReadContentAsync(joinResponse, cancellationToken)).SanitizeLogValue());

                    return TelephonyResult.Failed(S["Telnyx could not merge the calls."].Value);
                }
            }

            return TelephonyResult.Success(BuildCall(
                primaryCallId,
                CallState.Connected,
                new Dictionary<string, object>
                {
                    ["isConference"] = true,
                    ["conferenceId"] = conferenceId,
                    ["participantCount"] = callIds.Count,
                }));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while merging Telnyx calls.");

            return TelephonyResult.Failed(S["Telnyx could not merge the calls."].Value);
        }
    }

    /// <inheritdoc/>
    public async Task<TelephonyCallLookupResult> GetCallStateAsync(string callId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(callId))
        {
            return new TelephonyCallLookupResult
            {
                Succeeded = false,
                Error = S["A call id is required to query the call state."].Value,
            };
        }

        if (!_options.IsConfigured)
        {
            return new TelephonyCallLookupResult
            {
                Succeeded = false,
                Error = NotConfigured().Error,
            };
        }

        try
        {
            using var client = CreateClient();
            using var response = await client.GetAsync($"calls/{Uri.EscapeDataString(callId)}", cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new TelephonyCallLookupResult
                {
                    Succeeded = true,
                    Found = false,
                };
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Telnyx rejected a call-state lookup for call {CallId} with status code {StatusCode}.", callId.SanitizeLogValue(), response.StatusCode);

                return new TelephonyCallLookupResult
                {
                    Succeeded = false,
                    Error = S["Telnyx could not query the call state."].Value,
                };
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var isAlive = document.RootElement.TryGetProperty("data", out var data) &&
                data.ValueKind == JsonValueKind.Object &&
                data.TryGetProperty("is_alive", out var aliveElement) &&
                aliveElement.ValueKind == JsonValueKind.True;

            var call = BuildCall(callId, isAlive ? CallState.Connected : CallState.Disconnected, metadata: null);

            return new TelephonyCallLookupResult
            {
                Succeeded = true,
                Found = true,
                Call = call,
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while querying the Telnyx call state for call {CallId}.", callId.SanitizeLogValue());

            return new TelephonyCallLookupResult
            {
                Succeeded = false,
                Error = S["Telnyx could not query the call state."].Value,
            };
        }
    }

    private async Task<TelephonyResult> ExecuteActionAsync(
        string callId,
        string action,
        IDictionary<string, object> body,
        Func<TelephonyCall> onSuccess,
        CancellationToken cancellationToken,
        bool succeedWhenMissing = false)
    {
        if (string.IsNullOrWhiteSpace(callId))
        {
            return TelephonyResult.Failed(S["A call identifier is required."].Value);
        }

        if (!_options.IsConfigured)
        {
            return NotConfigured();
        }

        try
        {
            using var client = CreateClient();
            using var content = body is null
                ? JsonContent.Create(new Dictionary<string, object>(), options: TelnyxJsonSerializerOptions.Default)
                : JsonContent.Create(body, options: TelnyxJsonSerializerOptions.Default);
            using var response = await client.PostAsync(
                $"calls/{Uri.EscapeDataString(callId)}/actions/{action}",
                content,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                if (succeedWhenMissing && response.StatusCode == HttpStatusCode.NotFound)
                {
                    return TelephonyResult.Success(onSuccess?.Invoke());
                }

                var payload = await SafeReadContentAsync(response, cancellationToken);

                _logger.LogError(
                    "Telnyx rejected the '{Action}' request for call {CallId} with status code {StatusCode}. Response: {Response}",
                    action,
                    callId.SanitizeLogValue(),
                    response.StatusCode,
                    payload.SanitizeLogValue());

                return TelephonyResult.Failed(S["Telnyx could not complete the requested operation."].Value);
            }

            return TelephonyResult.Success(onSuccess?.Invoke());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while performing the Telnyx '{Action}' operation.", action);

            return TelephonyResult.Failed(S["Telnyx could not complete the requested operation."].Value);
        }
    }

    private static TelephonyResult RequireCallId(string callId, string message)
        => string.IsNullOrWhiteSpace(callId) ? TelephonyResult.Failed(message) : null;

    private TelephonyResult NotConfigured()
        => TelephonyResult.Failed(S["The Telnyx provider is not enabled or fully configured."].Value);

    private static TelephonyCall BuildCall(
        string callId,
        CallState state,
        IDictionary<string, object> metadata = null,
        CallDirection direction = CallDirection.Outbound,
        bool isMuted = false,
        bool isOnHold = false)
        => new()
        {
            CallId = callId,
            State = state,
            IsMuted = isMuted,
            IsOnHold = isOnHold,
            Direction = direction,
            ProviderName = TelnyxConstants.ProviderTechnicalName,
            Metadata = metadata is null
                ? new Dictionary<string, object>()
                : new Dictionary<string, object>(metadata, StringComparer.OrdinalIgnoreCase),
        };

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient(TelnyxConstants.ProviderTechnicalName);
        client.BaseAddress = new Uri(_options.ApiBaseUrl);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        return client;
    }

    private static async Task<string> ReadDataStringAsync(HttpResponseMessage response, string propertyName, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (document.RootElement.TryGetProperty("data", out var data) &&
                data.ValueKind == JsonValueKind.Object &&
                data.TryGetProperty(propertyName, out var value))
            {
                return value.ValueKind == JsonValueKind.Number ? value.GetRawText() : value.GetString();
            }
        }
        catch (JsonException)
        {
            // Ignore malformed responses and fall back to a generated identifier below.
        }

        return Guid.NewGuid().ToString("N");
    }

    private static async Task<string> SafeReadContentAsync(HttpResponseMessage response, CancellationToken cancellationToken)
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
            return string.Empty;
        }
    }
}
