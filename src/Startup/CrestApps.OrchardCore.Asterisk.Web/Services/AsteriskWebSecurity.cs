using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace CrestApps.OrchardCore.Asterisk.Web.Services;

/// <summary>
/// Keeps the sample's call-control dashboard to this machine: it only starts in Development, answers only requests from
/// this machine addressed to this machine (so a page elsewhere can't reach it through DNS rebinding), and its API and the
/// live dashboard's SignalR hub need the key the dashboard page carries, which a page on another origin can't read, and
/// refuse a request whose <c>Origin</c> is another page.
/// </summary>
internal static class AsteriskWebSecurity
{
    public const string ApiKeyHeaderName = "X-Asterisk-Web-Key";

    public static void EnsureDevelopmentOnly(string environmentName)
    {
        if (!string.Equals(environmentName, Environments.Development, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The Asterisk Web sample is development-only and cannot start outside the Development environment.");
        }
    }

    /// <summary>
    /// Returns whether the request comes from this machine, names this machine as its host, and, for the API and the hub,
    /// carries the key and comes from no other page.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <param name="apiKey">The key the dashboard page carries.</param>
    public static bool IsAllowedRequest(HttpContext context, string apiKey)
    {
        ArgumentNullException.ThrowIfNull(context);

        var remote = context.Connection.RemoteIpAddress;

        if (remote is not null)
        {
            if (remote.IsIPv4MappedToIPv6)
            {
                remote = remote.MapToIPv4();
            }

            if (!IPAddress.IsLoopback(remote))
            {
                return false;
            }
        }

        if (!IsThisMachine(context.Request.Host.Host))
        {
            return false;
        }

        var isApi = context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase);
        var isHub = context.Request.Path.StartsWithSegments("/hubs", StringComparison.OrdinalIgnoreCase);

        if (!isApi && !isHub)
        {
            return true;
        }

        // A browser opens a WebSocket to this machine from any page and doesn't ask first, so the live dashboard's hub
        // needs the key as much as the API does, and a request that says it comes from another page is refused.
        if (!IsFromThisOrigin(context.Request))
        {
            return false;
        }

        var given = context.Request.Headers[ApiKeyHeaderName].ToString();

        if (isHub && string.IsNullOrEmpty(given))
        {
            given = ReadHubKey(context.Request);
        }

        return !string.IsNullOrEmpty(apiKey) &&
            !string.IsNullOrEmpty(given) &&
            CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(given), Encoding.UTF8.GetBytes(apiKey));
    }

    // A browser names the page a WebSocket or a cross-origin request comes from; when it does, it must be the dashboard.
    private static bool IsFromThisOrigin(HttpRequest request)
    {
        var origin = request.Headers.Origin.ToString();

        return string.IsNullOrEmpty(origin) ||
            string.Equals(origin, $"{request.Scheme}://{request.Host.Value}", StringComparison.OrdinalIgnoreCase);
    }

    // SignalR's browser client can't put a header on a WebSocket: it sends the key it is given as the access_token query
    // value there, and as a bearer token on its other requests.
    private static string ReadHubKey(HttpRequest request)
    {
        const string BearerPrefix = "Bearer ";

        var authorization = request.Headers.Authorization.ToString();

        if (authorization.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return authorization[BearerPrefix.Length..].Trim();
        }

        return request.Query["access_token"].ToString();
    }

    private static bool IsThisMachine(string host)
        => string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
            (IPAddress.TryParse(host?.Trim('[', ']'), out var address) && IPAddress.IsLoopback(address));
}

/// <summary>
/// The key the dashboard's API calls carry, new each time the sample starts.
/// </summary>
internal sealed class AsteriskWebApiKey
{
    public string Value { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
}
