using System.Security.Cryptography;
using System.Text;
using Cysharp.Text;
using Microsoft.AspNetCore.Http;
using OrchardCore;

namespace CrestApps.OrchardCore.AI.Core;

/// <summary>
/// Provides extension methods for generating a hashed client identifier from an IP address and user agent.
/// </summary>
public static class ClientIpAddressAccessorExtensions
{
    /// <summary>
    /// Generates a SHA-256-based client identifier by combining the client's IP address with the request's user agent.
    /// </summary>
    /// <param name="accessor">The client IP address accessor.</param>
    /// <param name="httpContext">The current HTTP context providing the user agent header.</param>
    /// <returns>A hex-encoded hash string identifying the client, or <see langword="null"/> if the IP address is unavailable.</returns>
    public static async Task<string> GetClientIdAsync(this IClientIPAddressAccessor accessor, HttpContext httpContext)
    {
        var ipAddress = await accessor.GetIPAddressAsync();

        if (ipAddress is null)
        {
            return null;
        }

        var inputBytes = Encoding.UTF8.GetBytes($"{ipAddress}-{httpContext.Request.Headers.UserAgent}");
        var hashBytes = SHA256.HashData(inputBytes);
        using var sb = ZString.CreateStringBuilder();

        foreach (var b in hashBytes)
        {
            sb.Append(b.ToString("x2")); // Format each byte as a hex value
        }

        return sb.ToString();
    }
}
