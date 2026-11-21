using System.Security.Cryptography;
using System.Text;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Services;

/// <summary>
/// The parts of an agent platform's profile address it is known by: its origin, its host, and the key of its origin that
/// the unregistered platforms of one origin share their part of the allowance by.
/// </summary>
public static class AgentPlatformAddress
{
    /// <summary>
    /// The number of characters of an origin key: 16 bytes of the origin's SHA-256 hash, in hexadecimal.
    /// </summary>
    public const int OriginKeyLength = 32;

    /// <summary>
    /// The longest host a platform is known by: the longest DNS name, without its trailing dot.
    /// </summary>
    public const int MaxHostLength = 253;

    /// <summary>
    /// Returns an address's origin (scheme, host and port, in lower case, without the trailing dot of a fully qualified
    /// name), or <see langword="null"/> when it isn't one or its host is longer than <see cref="MaxHostLength"/>.
    /// </summary>
    /// <param name="url">The address.</param>
    public static string GetOrigin(string url)
        => TryParse(url, out var uri, out var host) ? $"{uri.Scheme}://{host}:{uri.Port}".ToLowerInvariant() : null;

    /// <summary>
    /// Returns the key of an address's origin, or <see langword="null"/> when it isn't one.
    /// </summary>
    /// <param name="url">The address.</param>
    public static string GetOriginKey(string url)
        => GetOrigin(url) is { } origin
            ? Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(origin)).AsSpan(0, OriginKeyLength / 2))
            : null;

    /// <summary>
    /// Returns an address's host, as <see cref="NormalizeHost"/> writes it, or <see langword="null"/> when it has none or
    /// none a DNS name can be (longer than <see cref="MaxHostLength"/>).
    /// </summary>
    /// <param name="url">The address.</param>
    public static string GetHost(string url)
        => TryParse(url, out _, out var host) && host.Length > 0 ? host : null;

    /// <summary>
    /// Returns a host in the form platforms are found by: its punycode form (<see cref="Uri.IdnHost"/>), in lower case,
    /// without the trailing dot of a fully qualified name, which names the same host.
    /// </summary>
    /// <param name="host">The host, as <see cref="Uri.IdnHost"/> gives it.</param>
    public static string NormalizeHost(string host)
        => host?.TrimEnd('.').ToLowerInvariant();

    // An address with a host no DNS name can have, a Unicode host whose punycode form is too long (Uri.IdnHost throws) or
    // a host over 253 characters (which the 255-character index column refuses on SQL Server and PostgreSQL), has no host
    // or origin, so a platform saved with one can still be saved and indexed.
    private static bool TryParse(string url, out Uri uri, out string host)
    {
        host = null;

        if (!Uri.TryCreate(url, UriKind.Absolute, out uri))
        {
            return false;
        }

        try
        {
            host = NormalizeHost(uri.IdnHost);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            return false;
        }

        return host.Length <= MaxHostLength;
    }
}
