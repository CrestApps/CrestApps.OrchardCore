using System.Security.Cryptography;
using System.Text;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Services;

/// <summary>
/// Makes and checks agent platforms' API keys. Only a key's hash is kept.
/// </summary>
public static class AgentPlatformApiKeys
{
    /// <summary>
    /// The prefix every key starts with, so a leaked one is recognizable.
    /// </summary>
    public const string Prefix = "agk_";

    /// <summary>
    /// Returns a new random key.
    /// </summary>
    public static string Create()
        => Prefix + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));

    /// <summary>
    /// Returns the hash a key is kept as.
    /// </summary>
    /// <param name="apiKey">The key.</param>
    public static string Hash(string apiKey)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(apiKey ?? string.Empty)));

    /// <summary>
    /// Returns the last characters of a key, to tell keys apart.
    /// </summary>
    /// <param name="apiKey">The key.</param>
    public static string Hint(string apiKey)
        => string.IsNullOrEmpty(apiKey) || apiKey.Length < 4 ? null : apiKey.Substring(apiKey.Length - 4);
}
