using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Asterisk.Services;

internal abstract class AsteriskTelephonyProviderBase : ITelephonyProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    protected AsteriskTelephonyProviderBase(
        IHttpClientFactory httpClientFactory,
        IClock clock,
        ILogger logger,
        IStringLocalizer stringLocalizer)
    {
        _httpClientFactory = httpClientFactory;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    protected IStringLocalizer S { get; }

    protected abstract string ProviderName { get; }

    public abstract LocalizedString Name { get; }

    public TelephonyCapabilities Capabilities
    {
        get
        {
            return TelephonyCapabilities.Dial |
                TelephonyCapabilities.Hangup |
                TelephonyCapabilities.Hold |
                TelephonyCapabilities.Resume |
                TelephonyCapabilities.Mute |
                TelephonyCapabilities.Merge |
                TelephonyCapabilities.SendDigits;
        }
    }

    public async Task<TelephonyResult> DialAsync(DialRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.To))
        {
            return TelephonyResult.Failed(S["A destination phone number or endpoint is required to place a call."].Value);
        }

        var settings = await GetResolvedSettingsAsync(cancellationToken);

        if (!IsConfigured(settings))
        {
            return NotConfigured();
        }

        var endpoint = AsteriskSettingsUtilities.ResolveEndpoint(settings.EndpointTemplate, request.To);

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return TelephonyResult.Failed(S["The configured endpoint template did not produce a valid Asterisk endpoint."].Value);
        }

        var callerId = string.IsNullOrWhiteSpace(request.From)
            ? settings.OutboundCallerId
            : request.From;

        var query = new Dictionary<string, string>
        {
            ["endpoint"] = endpoint,
            ["app"] = settings.ApplicationName,
            ["timeout"] = AsteriskSettingsUtilities.ToInvariantString(settings.TimeoutSeconds),
        };

        if (!string.IsNullOrWhiteSpace(callerId))
        {
            query["callerId"] = callerId;
        }

        try
        {
            using var response = await SendAsync(settings, HttpMethod.Post, "channels", query, request.Metadata, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Asterisk rejected a dial request for provider {ProviderName} with status code {StatusCode}.", ProviderName, response.StatusCode);

                return TelephonyResult.Failed(S["Asterisk could not place the call."].Value);
            }

            var callId = await ReadIdAsync(response, cancellationToken);

            var call = new TelephonyCall
            {
                CallId = callId ?? request.To,
                From = callerId,
                To = request.To,
                State = CallState.Connecting,
                Direction = CallDirection.Outbound,
                ProviderName = ProviderName,
                StartedUtc = _clock.UtcNow,
            };

            return TelephonyResult.Success(call);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while placing an Asterisk call for provider {ProviderName}.", ProviderName);

            return TelephonyResult.Failed(S["Asterisk could not place the call. Error: {0}", ex.Message].Value);
        }
    }

    public Task<TelephonyResult> HangupAsync(CallReference call, CancellationToken cancellationToken = default)
        => ExecuteCallActionAsync(
            call?.CallId,
            HttpMethod.Delete,
            "channels/{callId}",
            null,
            () => BuildCall(call?.CallId, CallState.Disconnected),
            S["Asterisk could not end the call."].Value,
            S["A call id is required to end the call."].Value,
            cancellationToken);

    public Task<TelephonyResult> HoldAsync(CallReference call, CancellationToken cancellationToken = default)
        => ExecuteCallActionAsync(
            call?.CallId,
            HttpMethod.Post,
            "channels/{callId}/hold",
            null,
            () => BuildCall(call?.CallId, CallState.OnHold, isOnHold: true),
            S["Asterisk could not place the call on hold."].Value,
            S["A call id is required to hold the call."].Value,
            cancellationToken);

    public Task<TelephonyResult> ResumeAsync(CallReference call, CancellationToken cancellationToken = default)
        => ExecuteCallActionAsync(
            call?.CallId,
            HttpMethod.Delete,
            "channels/{callId}/hold",
            null,
            () => BuildCall(call?.CallId, CallState.Connected),
            S["Asterisk could not resume the call."].Value,
            S["A call id is required to resume the call."].Value,
            cancellationToken);

    public Task<TelephonyResult> MuteAsync(CallReference call, CancellationToken cancellationToken = default)
        => ExecuteCallActionAsync(
            call?.CallId,
            HttpMethod.Post,
            "channels/{callId}/mute",
            new Dictionary<string, string> { ["direction"] = "both" },
            () => BuildCall(call?.CallId, CallState.Connected, isMuted: true),
            S["Asterisk could not mute the call."].Value,
            S["A call id is required to mute the call."].Value,
            cancellationToken);

    public Task<TelephonyResult> UnmuteAsync(CallReference call, CancellationToken cancellationToken = default)
        => ExecuteCallActionAsync(
            call?.CallId,
            HttpMethod.Delete,
            "channels/{callId}/mute",
            new Dictionary<string, string> { ["direction"] = "both" },
            () => BuildCall(call?.CallId, CallState.Connected),
            S["Asterisk could not unmute the call."].Value,
            S["A call id is required to unmute the call."].Value,
            cancellationToken);

    public Task<TelephonyResult> TransferAsync(TransferRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(TelephonyResult.Failed(S["The {0} provider does not support call transfers.", Name.Value].Value));

    public async Task<TelephonyResult> MergeAsync(MergeRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.PrimaryCallId) || string.IsNullOrWhiteSpace(request.SecondaryCallId))
        {
            return TelephonyResult.Failed(S["Both call ids are required to merge calls."].Value);
        }

        var settings = await GetResolvedSettingsAsync(cancellationToken);

        if (!IsConfigured(settings))
        {
            return NotConfigured();
        }

        try
        {
            var createBridgeQuery = new Dictionary<string, string>
            {
                ["type"] = "mixing",
            };

            if (!string.IsNullOrWhiteSpace(request.ConferenceName))
            {
                createBridgeQuery["name"] = request.ConferenceName;
            }

            using var createBridgeResponse = await SendAsync(settings, HttpMethod.Post, "bridges", createBridgeQuery, null, cancellationToken);

            if (!createBridgeResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Asterisk rejected a bridge creation request for provider {ProviderName} with status code {StatusCode}.", ProviderName, createBridgeResponse.StatusCode);

                return TelephonyResult.Failed(S["Asterisk could not merge the calls."].Value);
            }

            var bridgeId = await ReadIdAsync(createBridgeResponse, cancellationToken);

            if (string.IsNullOrWhiteSpace(bridgeId))
            {
                _logger.LogError("Asterisk did not return a bridge id when merging calls for provider {ProviderName}.", ProviderName);

                return TelephonyResult.Failed(S["Asterisk could not merge the calls."].Value);
            }

            var addChannelQuery = new Dictionary<string, string>
            {
                ["channel"] = $"{request.PrimaryCallId},{request.SecondaryCallId}",
            };

            using var addChannelResponse = await SendAsync(
                settings,
                HttpMethod.Post,
                $"bridges/{Uri.EscapeDataString(bridgeId)}/addChannel",
                addChannelQuery,
                null,
                cancellationToken);

            if (!addChannelResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Asterisk rejected a bridge add-channel request for provider {ProviderName} with status code {StatusCode}.", ProviderName, addChannelResponse.StatusCode);

                return TelephonyResult.Failed(S["Asterisk could not merge the calls."].Value);
            }

            return TelephonyResult.Success(BuildCall(request.PrimaryCallId, CallState.Connected));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while merging Asterisk calls for provider {ProviderName}.", ProviderName);

            return TelephonyResult.Failed(S["Asterisk could not merge the calls. Error: {0}", ex.Message].Value);
        }
    }

    public Task<TelephonyResult> SendDigitsAsync(SendDigitsRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Digits))
        {
            return Task.FromResult(TelephonyResult.Failed(S["Digits are required."].Value));
        }

        return ExecuteCallActionAsync(
            request.CallId,
            HttpMethod.Post,
            "channels/{callId}/dtmf",
            new Dictionary<string, string> { ["dtmf"] = request.Digits },
            () => null,
            S["Asterisk could not send the digits."].Value,
            S["A call id is required to send digits."].Value,
            cancellationToken);
    }

    public Task<TelephonyResult> AnswerAsync(CallReference call, CancellationToken cancellationToken = default)
        => Task.FromResult(TelephonyResult.Failed(S["The {0} provider does not support answering inbound calls in the soft phone.", Name.Value].Value));

    public Task<TelephonyResult> RejectAsync(CallReference call, CancellationToken cancellationToken = default)
        => Task.FromResult(TelephonyResult.Failed(S["The {0} provider does not support rejecting inbound calls in the soft phone.", Name.Value].Value));

    public Task<TelephonyResult> SendToVoicemailAsync(CallReference call, CancellationToken cancellationToken = default)
        => Task.FromResult(TelephonyResult.Failed(S["The {0} provider does not support sending calls to voicemail from the soft phone.", Name.Value].Value));

    public async Task<TelephonyClientCredentials> GetClientCredentialsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetResolvedSettingsAsync(cancellationToken);

        if (!IsConfigured(settings))
        {
            return null;
        }

        return new TelephonyClientCredentials
        {
            ProviderName = ProviderName,
            Settings = new Dictionary<string, string>(),
        };
    }

    protected abstract ValueTask<AsteriskResolvedSettings> GetResolvedSettingsAsync(CancellationToken cancellationToken);

    private async Task<TelephonyResult> ExecuteCallActionAsync(
        string callId,
        HttpMethod method,
        string pathTemplate,
        IDictionary<string, string> query,
        Func<TelephonyCall> onSuccess,
        string errorMessage,
        string missingCallIdMessage,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(callId))
        {
            return TelephonyResult.Failed(missingCallIdMessage);
        }

        var settings = await GetResolvedSettingsAsync(cancellationToken);

        if (!IsConfigured(settings))
        {
            return NotConfigured();
        }

        try
        {
            var path = pathTemplate.Replace("{callId}", Uri.EscapeDataString(callId), StringComparison.Ordinal);

            using var response = await SendAsync(settings, method, path, query, null, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Asterisk rejected a call action request for provider {ProviderName}. Path: {Path}. Status code: {StatusCode}.",
                    ProviderName,
                    path,
                    response.StatusCode);

                return TelephonyResult.Failed(errorMessage);
            }

            return TelephonyResult.Success(onSuccess?.Invoke());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while executing an Asterisk call action for provider {ProviderName}.", ProviderName);

            return TelephonyResult.Failed(S["{0} Error: {1}", errorMessage, ex.Message].Value);
        }
    }

    private HttpClient CreateClient(AsteriskResolvedSettings settings)
    {
        var client = _httpClientFactory.CreateClient(AsteriskConstants.HttpClientName);
        client.BaseAddress = new Uri(settings.BaseUrl, UriKind.Absolute);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.UserName}:{settings.Password}")));

        return client;
    }

    private TelephonyCall BuildCall(
        string callId,
        CallState state,
        bool isOnHold = false,
        bool isMuted = false)
        => new()
        {
            CallId = callId,
            State = state,
            Direction = CallDirection.Outbound,
            IsOnHold = isOnHold,
            IsMuted = isMuted,
            ProviderName = ProviderName,
        };

    private static bool IsConfigured(AsteriskResolvedSettings settings)
        => settings is not null &&
            settings.IsEnabled &&
            !string.IsNullOrWhiteSpace(settings.BaseUrl) &&
            !string.IsNullOrWhiteSpace(settings.UserName) &&
            !string.IsNullOrWhiteSpace(settings.Password) &&
            !string.IsNullOrWhiteSpace(settings.ApplicationName);

    private TelephonyResult NotConfigured()
        => TelephonyResult.Failed(S["The {0} provider is not configured.", Name.Value].Value);

    private async Task<HttpResponseMessage> SendAsync(
        AsteriskResolvedSettings settings,
        HttpMethod method,
        string relativePath,
        IDictionary<string, string> query,
        IDictionary<string, string> variables,
        CancellationToken cancellationToken)
    {
        var client = CreateClient(settings);
        var url = relativePath;

        if (query is { Count: > 0 })
        {
            url = QueryHelpers.AddQueryString(url, query);
        }

        using var request = new HttpRequestMessage(method, url);

        if (variables is { Count: > 0 })
        {
            request.Content = JsonContent.Create(new { variables });
        }

        return await client.SendAsync(request, cancellationToken);
    }

    private static async Task<string> ReadIdAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        using var document = JsonDocument.Parse(content);

        if (!document.RootElement.TryGetProperty("id", out var idElement))
        {
            return null;
        }

        return idElement.ValueKind switch
        {
            JsonValueKind.String => idElement.GetString(),
            JsonValueKind.Number => idElement.GetRawText(),
            _ => null,
        };
    }
}
