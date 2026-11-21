using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using CrestApps.OrchardCore.DataConnectors.Models;
using CrestApps.OrchardCore.DataConnectors.Telemetry;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.DataConnectors.Http;

/// <summary>
/// Sends the requests of HTTP-based connectors: adds the connection's credentials and headers, retries throttled and
/// failed calls a few times, follows redirects only on the same origin, and turns failures into
/// <see cref="DataConnectorException"/>s whose messages never hold a secret or a query string.
/// </summary>
/// <remarks>
/// Each retry is counted on <c>crestapps.dataconnector.http.retries</c> and <c>crestapps.retry.attempts</c> and
/// logged at Debug with its attempt number, never with the URL. The URL, without its query string, is logged at Debug
/// only, because an API key can be in the query string.
/// </remarks>
internal sealed class DataConnectorHttpClient
{
    private const int _maxAttempts = 4;
    private const int _maxRedirects = 3;
    private static readonly TimeSpan _maxRetryDelay = TimeSpan.FromSeconds(30);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HttpRequestAuthenticator _authenticator;
    private readonly DataConnectorNetworkOptions _networkOptions;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    public DataConnectorHttpClient(
        IHttpClientFactory httpClientFactory,
        HttpRequestAuthenticator authenticator,
        IOptions<DataConnectorNetworkOptions> networkOptions,
        IClock clock,
        ILogger<DataConnectorHttpClient> logger = null)
    {
        _httpClientFactory = httpClientFactory;
        _authenticator = authenticator;
        _networkOptions = networkOptions.Value;
        _clock = clock;
        _logger = logger ?? NullLogger<DataConnectorHttpClient>.Instance;
    }

    /// <summary>
    /// Gets or sets how long to wait before a retry; tests replace it.
    /// </summary>
    internal Func<TimeSpan, CancellationToken, Task> Delay { get; set; } = Task.Delay;

    /// <summary>
    /// Returns the cache key of a connection's tokens, which changes when the connection does.
    /// </summary>
    public static string CacheKey(DataConnection connection)
        => $"{connection.ItemId}:{(connection.ModifiedUtc ?? connection.CreatedUtc).Ticks.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>
    /// Resolves a path or URL against a connection's base URL. The result must be an http or https address on the base
    /// URL's origin, whatever the value looked like, and a backslash in the path is refused: <c>\/host/x</c> resolved to
    /// <c>https://host/x</c> and took the connection's credentials there.
    /// </summary>
    public static Uri Resolve(HttpConnectionSettings settings, string pathOrUrl)
    {
        var baseUri = GetBaseUri(settings);

        if (string.IsNullOrWhiteSpace(pathOrUrl))
        {
            return baseUri;
        }

        var value = pathOrUrl.Trim();
        var pathEnd = value.IndexOfAny(['?', '#']);

        if ((pathEnd < 0 ? value : value[..pathEnd]).Contains('\\', StringComparison.Ordinal))
        {
            throw new DataConnectorException("The address can't hold a backslash; use forward slashes.");
        }

        Uri resolved;

        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute) && absolute.Scheme is "http" or "https")
        {
            resolved = absolute;
        }
        else
        {
            // A relative path is appended to the base URL's path, as a client library would.
            var root = baseUri.AbsoluteUri.EndsWith('/') ? baseUri : new Uri(baseUri.AbsoluteUri + "/");

            resolved = new Uri(root, value.TrimStart('/'));
        }

        EnsureOnConnectionServer(baseUri, resolved);

        return resolved;
    }

    /// <summary>
    /// Throws unless an address is http or https on the base URL's origin, the only place the connection's credentials
    /// and headers may be sent.
    /// </summary>
    public static void EnsureOnConnectionServer(Uri baseUri, Uri uri)
    {
        ArgumentNullException.ThrowIfNull(baseUri);
        ArgumentNullException.ThrowIfNull(uri);

        if (!uri.IsAbsoluteUri || uri.Scheme is not ("http" or "https") || !IsSameOrigin(baseUri, uri))
        {
            var authority = uri.IsAbsoluteUri ? uri.GetLeftPart(UriPartial.Authority) : "a relative address";

            throw new DataConnectorException($"The address {authority} isn't on the connection's server {baseUri.GetLeftPart(UriPartial.Authority)}.");
        }
    }

    /// <summary>
    /// Returns the base URL of a connection.
    /// </summary>
    public static Uri GetBaseUri(HttpConnectionSettings settings)
    {
        if (!Uri.TryCreate(settings?.BaseUrl?.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            throw new DataConnectorException("The connection's base URL must be an absolute http:// or https:// URL.");
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new DataConnectorException("The connection's base URL can't hold credentials; use its authentication settings.");
        }

        return uri;
    }

    /// <summary>
    /// Returns whether two URLs share scheme, host and port.
    /// </summary>
    public static bool IsSameOrigin(Uri left, Uri right)
        => string.Equals(left.Scheme, right.Scheme, StringComparison.OrdinalIgnoreCase) &&
           string.Equals(left.IdnHost, right.IdnHost, StringComparison.OrdinalIgnoreCase) &&
           left.Port == right.Port;

    /// <summary>
    /// Sends a request and returns the response, which the caller disposes. Unless <paramref name="ensureSuccess"/> is
    /// <see langword="false"/>, a response that isn't successful after the retries throws.
    /// </summary>
    public async Task<HttpResponseMessage> SendAsync(
        HttpConnectionSettings settings,
        IDataConnectorSecrets secrets,
        string cacheKey,
        HttpMethod method,
        Uri uri,
        Func<HttpContent> createContent,
        CancellationToken cancellationToken,
        bool ensureSuccess = true)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(secrets);
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(uri);

        // Checked again here, so no caller can send the connection's credentials and headers to another server.
        EnsureOnConnectionServer(GetBaseUri(settings), uri);

        return await SendWithRetriesAsync(settings, secrets, cacheKey, method, uri, createContent, new HttpCall(), ensureSuccess, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendWithRetriesAsync(
        HttpConnectionSettings settings,
        IDataConnectorSecrets secrets,
        string cacheKey,
        HttpMethod method,
        Uri uri,
        Func<HttpContent> createContent,
        HttpCall call,
        bool ensureSuccess,
        CancellationToken cancellationToken)
    {
        var origin = uri;
        var current = uri;
        var currentMethod = method;
        var redirects = 0;
        var refreshedToken = false;
        var timeoutSeconds = settings.TimeoutSeconds > 0 ? settings.TimeoutSeconds : 100;

        using var client = _httpClientFactory.CreateClient(DataConnectorConstants.HttpClientName);

        for (var attempt = 1; ; attempt++)
        {
            using var request = new HttpRequestMessage(currentMethod, current);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*", 0.8));

            foreach (var header in settings.Headers ?? [])
            {
                if (!string.IsNullOrWhiteSpace(header?.Name))
                {
                    request.Headers.TryAddWithoutValidation(header.Name.Trim(), header.Value ?? string.Empty);
                }
            }

            if (currentMethod != HttpMethod.Get && createContent is not null)
            {
                request.Content = createContent();
            }

            await _authenticator.ApplyAsync(request, settings.Authentication, secrets, cacheKey, cancellationToken);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            HttpResponseMessage response;
            var sentTimestamp = Stopwatch.GetTimestamp();

            call.Requests++;

            try
            {
                response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            }
            catch (HttpRequestException exception) when (attempt < _maxAttempts && exception.InnerException is not DataConnectorException)
            {
                LogNoAnswer(currentMethod, current, call.Requests, DataConnectorsTelemetry.RetryNetwork);
                await RetryAsync(DataConnectorsTelemetry.RetryNetwork, null, attempt, RetryDelay(attempt, null), cancellationToken);

                continue;
            }
            catch (HttpRequestException exception)
            {
                throw new DataConnectorException($"Couldn't reach {Describe(current)}: {exception.Message}", exception) { IsTransient = exception.InnerException is not DataConnectorException };
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                LogNoAnswer(currentMethod, current, call.Requests, DataConnectorsTelemetry.RetryTimeout);

                if (attempt < _maxAttempts)
                {
                    await RetryAsync(DataConnectorsTelemetry.RetryTimeout, null, attempt, RetryDelay(attempt, null), cancellationToken);

                    continue;
                }

                throw new DataConnectorException($"{Describe(current)} didn't answer within {timeoutSeconds} seconds.", exception) { IsTransient = true };
            }

            var status = (int)response.StatusCode;

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.HttpResponded(currentMethod.Method, Describe(current), status, (long)Stopwatch.GetElapsedTime(sentTimestamp).TotalMilliseconds, call.Requests);
            }

            if (response.IsSuccessStatusCode)
            {
                return response;
            }

            if (status is >= 300 and < 400 && response.Headers.Location is { } location)
            {
                response.Dispose();

                var next = location.IsAbsoluteUri ? location : new Uri(current, location);

                if (!IsSameOrigin(origin, next))
                {
                    throw new DataConnectorException($"{Describe(current)} redirected to another server ({next.GetLeftPart(UriPartial.Authority)}), which isn't followed.");
                }

                if (++redirects > _maxRedirects)
                {
                    throw new DataConnectorException($"{Describe(origin)} redirected too many times.");
                }

                current = next;

                if (response.StatusCode == HttpStatusCode.SeeOther)
                {
                    currentMethod = HttpMethod.Get;
                }

                // Following a redirect isn't a retry of a failure, so it is only counted here.
                DataConnectorsTelemetry.RecordHttpRetry(DataConnectorsTelemetry.RetryRedirect);
                attempt--;

                continue;
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized && !refreshedToken && HttpRequestAuthenticator.UsesTokens(settings.Authentication))
            {
                // The cached token may have been revoked or expired early: ask for a new one once.
                response.Dispose();
                _authenticator.Invalidate(cacheKey);
                refreshedToken = true;
                RecordRetry(DataConnectorsTelemetry.RetryTokenRefresh, status, attempt, TimeSpan.Zero);
                attempt--;

                continue;
            }

            if (IsTransient(response.StatusCode) && attempt < _maxAttempts)
            {
                var delay = RetryDelay(attempt, response.Headers.RetryAfter);
                var reason = GetRetryReason(response.StatusCode);
                response.Dispose();

                await RetryAsync(reason, status, attempt, delay, cancellationToken);

                continue;
            }

            if (!ensureSuccess)
            {
                return response;
            }

            using (response)
            {
                var detail = await ReadErrorAsync(response, cancellationToken);

                throw new DataConnectorException($"{Describe(current)} answered {status} {response.ReasonPhrase}{detail}") { IsTransient = IsTransient(response.StatusCode) };
            }
        }
    }

    /// <summary>
    /// Reads a response's body as JSON, up to the configured size.
    /// </summary>
    public async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await ReadStreamAsync(response, cancellationToken);

        try
        {
            return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        }
        catch (JsonException exception)
        {
            throw new DataConnectorException($"{Describe(response.RequestMessage?.RequestUri)} didn't answer with JSON: {exception.Message}", exception);
        }
    }

    /// <summary>
    /// Returns a response's body as a stream that fails past the configured size.
    /// </summary>
    public async Task<Stream> ReadStreamAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);

        var limit = _networkOptions.MaxResponseBytes > 0 ? _networkOptions.MaxResponseBytes : long.MaxValue;

        if (response.Content.Headers.ContentLength is { } length && length > limit)
        {
            throw new DataConnectorException($"{Describe(response.RequestMessage?.RequestUri)} answered {length} bytes, more than the {limit} bytes a call may read.");
        }

        return new LimitedStream(await response.Content.ReadAsStreamAsync(cancellationToken), limit);
    }

    /// <summary>
    /// Describes a URL without its query string, which may hold a key.
    /// </summary>
    public static string Describe(Uri uri)
        => uri is null ? "The server" : uri.GetLeftPart(UriPartial.Path);

    private static bool IsTransient(HttpStatusCode statusCode)
        => statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;

    private static string GetRetryReason(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.TooManyRequests => DataConnectorsTelemetry.RetryStatus429,
            HttpStatusCode.RequestTimeout => DataConnectorsTelemetry.RetryTimeout,
            _ => DataConnectorsTelemetry.RetryStatus5xx,
        };
    }

    private async Task RetryAsync(string reason, int? statusCode, int attempt, TimeSpan delay, CancellationToken cancellationToken)
    {
        RecordRetry(reason, statusCode, attempt, delay);

        await Delay(delay, cancellationToken);
    }

    // The warning names the attempt, the reason and the status, never the URL, which can hold an API key.
    private void RecordRetry(string reason, int? statusCode, int attempt, TimeSpan delay)
    {
        DataConnectorsTelemetry.RecordHttpRetry(reason);
        RetryTelemetry.RecordRetry(DataConnectorsTelemetry.RetryArea, reason, attempt + 1);
        _logger.HttpRetrying(reason, statusCode, attempt + 1, _maxAttempts, (long)delay.TotalMilliseconds);
    }

    private void LogNoAnswer(HttpMethod method, Uri uri, int request, string reason)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.HttpNoAnswer(method.Method, Describe(uri), request, reason);
        }
    }

    private TimeSpan RetryDelay(int attempt, RetryConditionHeaderValue retryAfter)
    {
        var delay = retryAfter?.Delta
            ?? (retryAfter?.Date is { } date ? date - new DateTimeOffset(DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc)) : TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)));

        if (delay < TimeSpan.Zero)
        {
            delay = TimeSpan.Zero;
        }

        return delay > _maxRetryDelay ? _maxRetryDelay : delay;
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(body))
            {
                return string.Empty;
            }

            body = body.ReplaceLineEndings(" ").Trim();

            return ": " + (body.Length > 300 ? body[..300] + "…" : body);
        }
        catch (HttpRequestException)
        {
            return string.Empty;
        }
    }

    // What one call sent and received, for the attempts histogram.
    private sealed class HttpCall
    {
        public int Requests { get; set; }
    }

    private sealed class LimitedStream : Stream
    {
        private readonly Stream _inner;
        private readonly long _limit;
        private long _read;

        public LimitedStream(Stream inner, long limit)
        {
            _inner = inner;
            _limit = limit;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => _read;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
            => Count(_inner.Read(buffer, offset, count));

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => Count(await _inner.ReadAsync(buffer, cancellationToken));

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => Count(await _inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken));

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await _inner.DisposeAsync();
            await base.DisposeAsync();
        }

        private int Count(int read)
        {
            _read += read;

            if (_read > _limit)
            {
                throw new DataConnectorException($"The response is larger than the {_limit} bytes a call may read.");
            }

            return read;
        }
    }
}
