using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace CrestApps.OrchardCore.Asterisk.Web.Services;

/// <summary>
/// Keeps the sample's call-control dashboard to this machine: it only starts in Development, answers only requests from
/// this machine addressed to this machine (so a page elsewhere can't reach it through DNS rebinding), and its API needs
/// the key the dashboard page carries, which a page on another origin can't read.
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
    /// Returns whether the request comes from this machine, names this machine as its host, and, for the API, carries the
    /// key.
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

        if (!context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var given = context.Request.Headers[ApiKeyHeaderName].ToString();

        return !string.IsNullOrEmpty(apiKey) &&
            !string.IsNullOrEmpty(given) &&
            CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(given), Encoding.UTF8.GetBytes(apiKey));
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
