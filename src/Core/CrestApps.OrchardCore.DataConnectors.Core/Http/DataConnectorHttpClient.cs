using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using CrestApps.OrchardCore.DataConnectors.Models;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.DataConnectors.Http;

/// <summary>
/// Sends the requests of HTTP-based connectors: adds the connection's credentials and headers, retries throttled and
/// failed calls a few times, follows redirects only on the same origin, and turns failures into
/// <see cref="DataConnectorException"/>s whose messages never hold a secret or a query string.
/// </summary>
internal sealed class DataConnectorHttpClient
{
    private const int _maxAttempts = 4;
    private const int _maxRedirects = 3;
    private static readonly TimeSpan _maxRetryDelay = TimeSpan.FromSeconds(30);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HttpRequestAuthenticator _authenticator;
    private readonly DataConnectorNetworkOptions _networkOptions;

    public DataConnectorHttpClient(
        IHttpClientFactory httpClientFactory,
        HttpRequestAuthenticator authenticator,
        IOptions<DataConnectorNetworkOptions> networkOptions)
    {
        _httpClientFactory = httpClientFactory;
        _authenticator = authenticator;
        _networkOptions = networkOptions.Value;
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
    /// Resolves a path or URL against a connection's base URL. An absolute URL must be on the base URL's origin.
    /// </summary>
    public static Uri Resolve(HttpConnectionSettings settings, string pathOrUrl)
    {
        var baseUri = GetBaseUri(settings);

        if (string.IsNullOrWhiteSpace(pathOrUrl))
        {
            return baseUri;
        }

        var value = pathOrUrl.Trim();

        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute) && absolute.Scheme is "http" or "https")
        {
            if (!IsSameOrigin(baseUri, absolute))
            {
                throw new DataConnectorException($"The address {absolute.GetLeftPart(UriPartial.Authority)} isn't on the connection's server {baseUri.GetLeftPart(UriPartial.Authority)}.");
            }

            return absolute;
        }

        // A relative path is appended to the base URL's path, as a client library would.
        var root = baseUri.AbsoluteUri.EndsWith('/') ? baseUri : new Uri(baseUri.AbsoluteUri + "/");

        return new Uri(root, value.TrimStart('/'));
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

            try
            {
                response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            }
            catch (HttpRequestException exception) when (attempt < _maxAttempts && exception.InnerException is not DataConnectorException)
            {
                await Delay(RetryDelay(attempt, null), cancellationToken);

                continue;
            }
            catch (HttpRequestException exception)
            {
                throw new DataConnectorException($"Couldn't reach {Describe(current)}: {exception.Message}", exception) { IsTransient = exception.InnerException is not DataConnectorException };
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                if (attempt < _maxAttempts)
                {
                    await Delay(RetryDelay(attempt, null), cancellationToken);

                    continue;
                }

                throw new DataConnectorException($"{Describe(current)} didn't answer within {timeoutSeconds} seconds.", exception) { IsTransient = true };
            }

            if (response.IsSuccessStatusCode)
            {
                return response;
            }

            var status = (int)response.StatusCode;

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

                attempt--;

                continue;
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized && !refreshedToken && HttpRequestAuthenticator.UsesTokens(settings.Authentication))
            {
                // The cached token may have been revoked or expired early: ask for a new one once.
                response.Dispose();
                _authenticator.Invalidate(cacheKey);
                refreshedToken = true;
                attempt--;

                continue;
            }

            if (IsTransient(response.StatusCode) && attempt < _maxAttempts)
            {
                var delay = RetryDelay(attempt, response.Headers.RetryAfter);
                response.Dispose();

                await Delay(delay, cancellationToken);

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

    private static TimeSpan RetryDelay(int attempt, RetryConditionHeaderValue retryAfter)
    {
        var delay = retryAfter?.Delta
            ?? (retryAfter?.Date is { } date ? date - DateTimeOffset.UtcNow : TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)));

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
