using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using OrchardCore;

namespace CrestApps.OrchardCore.AI.Core;

public static class ClientIpAddressAccessorExtensions
{
    public static async Task<string> GetClientIdAsync(this IClientIPAddressAccessor accessor, HttpContext httpContext)
    {
        var ipAddress = await accessor.GetIPAddressAsync();

        if (ipAddress is null)
        {
            return null;
        }

        var inputBytes = Encoding.UTF8.GetBytes($"{ipAddress}-{httpContext.Request.Headers.UserAgent}");
        var hashBytes = SHA256.HashData(inputBytes);
        var sb = new StringBuilder();
        foreach (var b in hashBytes)
        {
            sb.Append(b.ToString("x2")); // Format each byte as a hex value
        }

        return sb.ToString();
    }
}
