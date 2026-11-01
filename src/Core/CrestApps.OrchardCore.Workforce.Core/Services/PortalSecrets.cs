using System.Security.Cryptography;
using System.Text;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Makes and hashes the portal's secrets: feed addresses and push endpoints.
/// </summary>
internal static class PortalSecrets
{
    public static string NewToken()
    {
        return Base64Url(RandomNumberGenerator.GetBytes(32));
    }

    public static string Hash(string value)
    {
        return string.IsNullOrEmpty(value) ? null : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    public static string Base64Url(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
