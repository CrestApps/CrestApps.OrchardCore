using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.WebSockets;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Opens bidirectional media sessions for Telnyx calls through Telnyx Media Streaming. Unlike Asterisk ARI External
/// Media, where the application binds a socket Asterisk streams to, Telnyx dials back to a WebSocket the application
/// hosts: the provider issues a <c>streaming_start</c> command pointing at the tenant's media-stream endpoint, then
/// awaits the socket Telnyx connects. The correlation token that ties the two together is held in a per-node
/// in-memory registry, so live media currently requires a single-node (or per-node addressable) deployment.
/// </summary>
internal sealed class TelnyxContactCenterVoiceMediaProvider : IContactCenterVoiceMediaProvider
{
    private readonly ISiteService _siteService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IContactCenterFeatureWorkManager _workManager;
    private readonly IWebSocketConnectionRegistry _registry;
    private readonly TelnyxOptions _options;
    private readonly ILogger _logger;
    private readonly TimeSpan _connectTimeout;

    public TelnyxContactCenterVoiceMediaProvider(
        ISiteService siteService,
        IHttpContextAccessor httpContextAccessor,
        IHttpClientFactory httpClientFactory,
        IContactCenterFeatureWorkManager workManager,
        IWebSocketConnectionRegistry registry,
        IOptionsMonitor<TelnyxOptions> options,
        ILogger<TelnyxContactCenterVoiceMediaProvider> logger,
        TimeSpan? connectTimeout = null)
    {
        _siteService = siteService;
        _httpContextAccessor = httpContextAccessor;
        _httpClientFactory = httpClientFactory;
        _workManager = workManager;
        _registry = registry;
        _options = options.CurrentValue;
        _logger = logger;
        _connectTimeout = connectTimeout ?? TimeSpan.FromSeconds(15);
    }

    /// <inheritdoc/>
    public string TechnicalName => TelnyxConstants.ProviderTechnicalName;

    /// <inheritdoc/>
    public async Task<IContactCenterVoiceMediaSession> OpenSessionAsync(
        ContactCenterVoiceMediaSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.ProviderCallId))
        {
            throw new ArgumentException("A provider call id is required.", nameof(request));
        }

        ValidatePreferredFormat(request.PreferredIncomingFormat);
        ValidatePreferredFormat(request.PreferredOutgoingFormat);

        if (!_options.IsConfigured)
        {
            throw new InvalidOperationException("The Telnyx provider is not configured.");
        }

        var callControlId = request.ProviderCallId.Trim();
        var streamBaseUrl = await ResolvePublicWebSocketBaseUrlAsync(request.Metadata);

        var workLease = _workManager.TryEnter(TelnyxConstants.ContactCenterMediaWorkPartition);

        if (workLease is null)
        {
            throw new InvalidOperationException("The Telnyx Contact Center media provider is quiescing.");
        }

        var token = CreateToken();
        var streamUrl = $"{streamBaseUrl}?t={Uri.EscapeDataString(token)}";
        var connection = await _registry.RegisterAsync(token, cancellationToken);

        try
        {
            await StartStreamingAsync(callControlId, streamUrl, token, cancellationToken);

            var webSocket = await AwaitConnectionAsync(token, connection, callControlId, cancellationToken);

            return new TelnyxContactCenterVoiceMediaSession(
                Guid.NewGuid().ToString("n"),
                callControlId,
                webSocket,
                workLease,
                connection,
                stopToken => StopStreamingAsync(callControlId, stopToken));
        }
        catch
        {
            await _registry.RemoveAsync(token, CancellationToken.None);

            try
            {
                using var cleanupCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));

                await StopStreamingAsync(callControlId, cleanupCancellation.Token);
            }
            catch (Exception cleanupException)
            {
                _logger.LogWarning(
                    cleanupException,
                    "Unable to stop a partially opened Telnyx media stream for call {CallControlId}.",
                    callControlId);
            }
            finally
            {
                workLease.Dispose();
            }

            throw;
        }
    }

    private async Task<System.Net.WebSockets.WebSocket> AwaitConnectionAsync(
        string token,
        WebSocketRendezvous connection,
        string callControlId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await connection.ConnectedTask.WaitAsync(_connectTimeout, cancellationToken);
        }
        catch (Exception exception) when (exception is TimeoutException or OperationCanceledException)
        {
            // We gave up waiting. Ensure the token can no longer be claimed, and clean up a socket that races in
            // late so the media-stream endpoint does not stay parked holding an orphaned connection.
            await _registry.RemoveAsync(token, CancellationToken.None);

            _ = connection.ConnectedTask.ContinueWith(
                task =>
                {
                    if (task.Status == TaskStatus.RanToCompletion)
                    {
                        task.Result.Abort();
                        task.Result.Dispose();
                    }

                    connection.Release();
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            connection.Abort();

            _logger.LogWarning(
                "Telnyx did not connect the media stream for call {CallControlId} within {Seconds} seconds.",
                callControlId,
                _connectTimeout.TotalSeconds);

            throw;
        }
    }

    private async Task StartStreamingAsync(
        string callControlId,
        string streamUrl,
        string token,
        CancellationToken cancellationToken)
    {
        var body = new Dictionary<string, object>
        {
            ["stream_url"] = streamUrl,
            ["stream_track"] = TelnyxConstants.MediaStreaming.Track,
            ["stream_codec"] = TelnyxConstants.MediaStreaming.Codec,
            ["stream_bidirectional_mode"] = TelnyxConstants.MediaStreaming.BidirectionalMode,
            ["stream_bidirectional_codec"] = TelnyxConstants.MediaStreaming.Codec,
            ["stream_bidirectional_target_legs"] = TelnyxConstants.MediaStreaming.BidirectionalTargetLegs,
            ["stream_auth_token"] = token,
            ["command_id"] = Guid.NewGuid().ToString(),
        };

        using var client = CreateClient();
        using var content = JsonContent.Create(body, options: TelnyxJsonSerializerOptions.Default);
        using var response = await client.PostAsync(
            $"calls/{Uri.EscapeDataString(callControlId)}/actions/streaming_start",
            content,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Telnyx rejected a media streaming_start for call {CallControlId} with status code {StatusCode}.",
                callControlId,
                response.StatusCode);

            throw new InvalidOperationException("Telnyx rejected the media streaming request.");
        }
    }

    private async Task StopStreamingAsync(string callControlId, CancellationToken cancellationToken)
    {
        using var client = CreateClient();
        using var content = JsonContent.Create(new Dictionary<string, object>(), options: TelnyxJsonSerializerOptions.Default);
        using var response = await client.PostAsync(
            $"calls/{Uri.EscapeDataString(callControlId)}/actions/streaming_stop",
            content,
            cancellationToken);

        if (!response.IsSuccessStatusCode &&
            response.StatusCode != System.Net.HttpStatusCode.NotFound &&
            response.StatusCode != System.Net.HttpStatusCode.UnprocessableEntity)
        {
            // 404/422 mean the call or stream is already gone, which is a successful stop for our purposes.
            _logger.LogWarning(
                "Telnyx returned {StatusCode} stopping the media stream for call {CallControlId}.",
                response.StatusCode,
                callControlId);
        }
    }

    private async Task<string> ResolvePublicWebSocketBaseUrlAsync(IDictionary<string, string> metadata)
    {
        // Resolve the public address Telnyx must dial back, most-trusted source first:
        //   1. an explicit caller override (the caller knows the reachable address);
        //   2. the tenant's configured canonical base URL (operator-controlled, and the same value the Telnyx
        //      webhook URL is built from) — this is the only source that works when the session is opened outside
        //      an HTTP request, such as from an AI/bot orchestrator or a background webhook processor;
        //   3. the current request's scheme/host as a convenience fallback. This deliberately reads the resolved
        //      Request.Host rather than the raw X-Forwarded-Host header: behind a proxy it reflects the external
        //      host only when OrchardCore's Reverse Proxy feature is enabled to validate the forwarded headers, so
        //      an untrusted client cannot inject the host Telnyx is told to stream call audio to.
        var baseUrl = ResolveOverrideBaseUrl(metadata)
            ?? (await _siteService.GetSiteSettingsAsync())?.BaseUrl
            ?? ResolveRequestBaseUrl();

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException(
                "Telnyx media streaming requires a public base URL. Configure the tenant site base URL, open the " +
                "session during a request served through a trusted reverse proxy, or pass the " +
                $"'{TelnyxConstants.MediaStreamPublicUrlMetadataKey}' metadata value.");
        }

        if (!Uri.TryCreate(baseUrl.TrimEnd('/'), UriKind.Absolute, out var parsed))
        {
            throw new InvalidOperationException($"The media streaming base URL '{baseUrl}' is not a valid absolute URL.");
        }

        var scheme = parsed.Scheme switch
        {
            "https" or "wss" => "wss",
            "http" or "ws" => "ws",
            _ => throw new InvalidOperationException($"The media streaming base URL scheme '{parsed.Scheme}' is not supported."),
        };

        var builder = new UriBuilder(parsed)
        {
            Scheme = scheme,
            Path = $"{parsed.AbsolutePath.TrimEnd('/')}/{TelnyxConstants.MediaStreamPath}",
            Query = string.Empty,
        };

        // UriBuilder emits the default port for the scheme; drop it so the URL stays clean.
        if (builder.Uri.IsDefaultPort)
        {
            builder.Port = -1;
        }

        return builder.Uri.ToString();
    }

    private static string ResolveOverrideBaseUrl(IDictionary<string, string> metadata)
    {
        if (metadata is not null &&
            metadata.TryGetValue(TelnyxConstants.MediaStreamPublicUrlMetadataKey, out var overrideUrl) &&
            !string.IsNullOrWhiteSpace(overrideUrl))
        {
            return overrideUrl.Trim();
        }

        return null;
    }

    private string ResolveRequestBaseUrl()
    {
        var request = _httpContextAccessor?.HttpContext?.Request;

        if (request is null || !request.Host.HasValue)
        {
            return null;
        }

        return $"{request.Scheme}://{request.Host}{request.PathBase}";
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient(TelnyxConstants.ProviderTechnicalName);
        client.BaseAddress = new Uri(_options.ApiBaseUrl);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        return client;
    }

    private static string CreateToken()
        => Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static void ValidatePreferredFormat(ContactCenterVoiceMediaFormat format)
    {
        if (format is null)
        {
            return;
        }

        if (format.Encoding is not ContactCenterVoiceMediaEncoding.Unknown and
            not ContactCenterVoiceMediaEncoding.MuLaw)
        {
            throw new NotSupportedException("The Telnyx media adapter currently supports only G.711 mu-law audio.");
        }

        if (format.SampleRate is not 0 and not 8_000)
        {
            throw new NotSupportedException("The Telnyx media adapter currently supports only an 8 kHz sample rate.");
        }

        if (format.Channels is not 0 and not 1)
        {
            throw new NotSupportedException("The Telnyx media adapter currently supports only mono audio.");
        }
    }
}
