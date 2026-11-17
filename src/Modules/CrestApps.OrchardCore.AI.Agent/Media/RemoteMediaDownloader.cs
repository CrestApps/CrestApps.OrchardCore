using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using CrestApps.OrchardCore.Core.Http;
using OrchardCore.FileStorage;

namespace CrestApps.OrchardCore.AI.Agent.Media;

/// <summary>
/// Fetches a file from a public http or https address that a model chose, for <see cref="ImportMediaFromUrlTool"/>.
/// </summary>
/// <remarks>
/// The address comes from a conversation, so it can be an attempt to make the site read its own network (server-side
/// request forgery). Three things stop that. Before every request, the host is resolved and the request is refused
/// when any of its addresses is a loopback, private, link-local or otherwise internal address. The request goes
/// through the shared egress guard (<see cref="EgressNetwork"/>), which checks the address again when the connection
/// opens, so a host name that changes its address in between is still refused. Redirects are not followed
/// automatically: each one is checked the same way, up to <see cref="MaxRedirects"/>. The download stops at the size
/// limit and at the time limit.
/// </remarks>
internal sealed class RemoteMediaDownloader
{
    /// <summary>
    /// The name of the HTTP client the downloader uses. It is registered with the egress guard as its handler.
    /// </summary>
    public const string HttpClientName = "CrestApps.OrchardCore.AI.Agent.MediaImport";

    /// <summary>
    /// The user agent the client sends. Some hosts answer no request that has none.
    /// </summary>
    public const string UserAgent = "MediaImport/1.0";

    /// <summary>
    /// The number of redirects the downloader follows.
    /// </summary>
    public const int MaxRedirects = 5;

    /// <summary>
    /// The time one download may take, redirects included.
    /// </summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(100);

    private const int CopyBufferSize = 81920;

    private const string InternalAddressMessage = "The address is on the site's own network (a loopback, private or link-local address), so it was not fetched. Only public addresses can be imported.";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITempDirectoryProvider _tempDirectoryProvider;
    private readonly Func<string, CancellationToken, Task<IPAddress[]>> _resolveHost;

    /// <summary>
    /// Initializes a new instance of the <see cref="RemoteMediaDownloader"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="tempDirectoryProvider">The provider of the tenant's folder for temporary files.</param>
    public RemoteMediaDownloader(
        IHttpClientFactory httpClientFactory,
        ITempDirectoryProvider tempDirectoryProvider)
        : this(httpClientFactory, tempDirectoryProvider, Dns.GetHostAddressesAsync)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RemoteMediaDownloader"/> class with its own host resolver, for tests.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="tempDirectoryProvider">The provider of the tenant's folder for temporary files.</param>
    /// <param name="resolveHost">Resolves a host name to its addresses.</param>
    internal RemoteMediaDownloader(
        IHttpClientFactory httpClientFactory,
        ITempDirectoryProvider tempDirectoryProvider,
        Func<string, CancellationToken, Task<IPAddress[]>> resolveHost)
    {
        _httpClientFactory = httpClientFactory;
        _tempDirectoryProvider = tempDirectoryProvider;
        _resolveHost = resolveHost;
    }

    /// <summary>
    /// Says why an address cannot be fetched, from the address alone: it is not an absolute http or https address, it
    /// carries a user name or password, or it names an internal host.
    /// </summary>
    /// <param name="url">The address the model gave.</param>
    /// <param name="uri">The parsed address, when it can be fetched.</param>
    /// <returns>The reason as a sentence, or <see langword="null"/> when the address can be fetched.</returns>
    public static string CheckUrl(string url, out Uri uri)
    {
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out uri))
        {
            return "The url must be a complete address, for example https://example.com/images/logo.png.";
        }

        return CheckUri(uri);
    }

    /// <summary>
    /// Fetches the file at an address into a temporary file.
    /// </summary>
    /// <param name="uri">The address, already checked with <see cref="CheckUrl"/>.</param>
    /// <param name="maxBytes">The largest size, in bytes, the file may have.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The fetched file, or the reason nothing was fetched. Dispose it to delete the temporary file.</returns>
    public async Task<RemoteMediaDownload> DownloadAsync(Uri uri, long maxBytes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(uri);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        timeout.CancelAfter(Timeout);

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            var current = uri;

            for (var hop = 0; hop <= MaxRedirects; hop++)
            {
                var refusal = CheckUri(current) ?? await CheckAddressesAsync(current, timeout.Token);

                if (refusal is not null)
                {
                    return RemoteMediaDownload.Failed(hop == 0
                        ? refusal
                        : $"The address redirects to '{current.GetComponents(UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.UriEscaped)}', which was not fetched. {refusal}");
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, current);
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);

                if (IsRedirect(response.StatusCode))
                {
                    var location = response.Headers.Location;

                    if (location is null)
                    {
                        return RemoteMediaDownload.Failed("The address answered with a redirect that names no address.");
                    }

                    current = location.IsAbsoluteUri
                        ? location
                        : new Uri(current, location);

                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    return RemoteMediaDownload.Failed($"The address answered with the status {(int)response.StatusCode}, so nothing was fetched.");
                }

                if (response.Content.Headers.ContentLength > maxBytes)
                {
                    return RemoteMediaDownload.Failed(DescribeTooLarge(maxBytes));
                }

                return await ReadAsync(response, current, maxBytes, timeout.Token);
            }

            return RemoteMediaDownload.Failed($"The address redirects more than {MaxRedirects} times, so nothing was fetched.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return RemoteMediaDownload.Failed($"The download took longer than {Timeout.TotalSeconds:0} seconds and was stopped.");
        }
        catch (HttpRequestException exception) when (IsRefusedByEgressGuard(exception))
        {
            // The egress guard refused the address when the connection opened.
            return RemoteMediaDownload.Failed(InternalAddressMessage);
        }
        catch (Exception exception) when (exception is HttpRequestException or SocketException or IOException or ArgumentException or UriFormatException)
        {
            // A host that does not resolve, a refused or dropped connection, or a redirect to something that is no
            // address. The details can name internal hosts, so they are not passed on.
            return RemoteMediaDownload.Failed("The address could not be reached, or the download was interrupted. Check the address and try again.");
        }
    }

    private static string CheckUri(Uri uri)
    {
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return "Only http and https addresses can be imported.";
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            return "An address that carries a user name or a password is not accepted.";
        }

        return EgressNetwork.IsInternalHost(uri)
            ? InternalAddressMessage
            : null;
    }

    private async Task<string> CheckAddressesAsync(Uri uri, CancellationToken cancellationToken)
    {
        // An address written as numbers was checked with the address itself.
        if (uri.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6)
        {
            return null;
        }

        var addresses = await _resolveHost(uri.IdnHost, cancellationToken);

        if (addresses is not { Length: > 0 })
        {
            return $"The host '{uri.Host}' could not be found.";
        }

        // Every address is checked: a host that also answers on an internal address is refused.
        return addresses.Any(EgressNetwork.IsInternal)
            ? InternalAddressMessage
            : null;
    }

    private async Task<RemoteMediaDownload> ReadAsync(HttpResponseMessage response, Uri source, long maxBytes, CancellationToken cancellationToken)
    {
        var target = new FileStream(
            _tempDirectoryProvider.GetTempFileName(),
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None,
            CopyBufferSize,
            FileOptions.DeleteOnClose | FileOptions.Asynchronous);

        var fetched = false;

        try
        {
            await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);

            var buffer = new byte[CopyBufferSize];
            var length = 0L;
            int read;

            while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
            {
                length += read;

                // The size the address announced is not trusted, so the bytes are counted.
                if (length > maxBytes)
                {
                    return RemoteMediaDownload.Failed(DescribeTooLarge(maxBytes));
                }

                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }

            if (length == 0)
            {
                return RemoteMediaDownload.Failed("The address returned an empty file, so nothing was stored.");
            }

            target.Position = 0;
            fetched = true;

            return RemoteMediaDownload.Fetched(target, length, response.Content.Headers.ContentType?.MediaType, source);
        }
        finally
        {
            if (!fetched)
            {
                await target.DisposeAsync();
            }
        }
    }

    private static bool IsRedirect(HttpStatusCode statusCode)
    {
        return statusCode is HttpStatusCode.MovedPermanently
            or HttpStatusCode.Found
            or HttpStatusCode.SeeOther
            or HttpStatusCode.TemporaryRedirect
            or HttpStatusCode.PermanentRedirect;
    }

    private static bool IsRefusedByEgressGuard(HttpRequestException exception)
    {
        // The handler wraps the refusal of the guard in its own connection error.
        return exception.Message.Contains(EgressNetwork.RefusedMessage, StringComparison.Ordinal) ||
            exception.InnerException?.Message.Contains(EgressNetwork.RefusedMessage, StringComparison.Ordinal) == true;
    }

    private static string DescribeTooLarge(long maxBytes)
        => $"The file is larger than the site's upload limit of {maxBytes} bytes, so it was not stored.";
}
