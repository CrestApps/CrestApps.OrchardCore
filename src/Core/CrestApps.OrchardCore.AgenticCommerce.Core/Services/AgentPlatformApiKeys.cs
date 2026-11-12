using CrestApps.OrchardCore.Core.Security;

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

    // The hash of an empty key, as before: an empty key never matches a stored hash.
    private const string EmptyHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    /// <summary>
    /// Returns a new random key.
    /// </summary>
    public static string Create()
        => Prefix + SecretTokens.Create();

    /// <summary>
    /// Returns the hash a key is kept as.
    /// </summary>
    /// <param name="apiKey">The key.</param>
    public static string Hash(string apiKey)
        => string.IsNullOrEmpty(apiKey) ? EmptyHash : SecretTokens.Hash(apiKey);

    /// <summary>
    /// Returns the last characters of a key, to tell keys apart.
    /// </summary>
    /// <param name="apiKey">The key.</param>
    public static string Hint(string apiKey)
        => string.IsNullOrEmpty(apiKey) || apiKey.Length < 4 ? null : apiKey.Substring(apiKey.Length - 4);
}
