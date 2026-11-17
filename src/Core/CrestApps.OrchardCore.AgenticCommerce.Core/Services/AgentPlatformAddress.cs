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
    /// Returns an address's origin (scheme, host and port, in lower case, without the trailing dot of a fully qualified
    /// name), or <see langword="null"/> when it isn't one.
    /// </summary>
    /// <param name="url">The address.</param>
    public static string GetOrigin(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri) ? $"{uri.Scheme}://{uri.IdnHost.TrimEnd('.')}:{uri.Port}".ToLowerInvariant() : null;

    /// <summary>
    /// Returns the key of an address's origin, or <see langword="null"/> when it isn't one.
    /// </summary>
    /// <param name="url">The address.</param>
    public static string GetOriginKey(string url)
        => GetOrigin(url) is { } origin
            ? Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(origin)).AsSpan(0, OriginKeyLength / 2))
            : null;

    /// <summary>
    /// Returns an address's host, as <see cref="NormalizeHost"/> writes it, or <see langword="null"/> when it isn't one.
    /// </summary>
    /// <param name="url">The address.</param>
    public static string GetHost(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri) && NormalizeHost(uri.IdnHost) is { Length: > 0 } host ? host : null;

    /// <summary>
    /// Returns a host in the form platforms are found by: its punycode form (<see cref="Uri.IdnHost"/>), in lower case,
    /// without the trailing dot of a fully qualified name, which names the same host.
    /// </summary>
    /// <param name="host">The host, as <see cref="Uri.IdnHost"/> gives it.</param>
    public static string NormalizeHost(string host)
        => host?.TrimEnd('.').ToLowerInvariant();
}
