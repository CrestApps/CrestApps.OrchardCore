using System.Security.Cryptography;
using System.Text;
using OrchardCore;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Mints and hashes the one-time access tokens that authorize a customer secure capture page. Only the SHA-256
/// hash of a token is ever persisted, so a leaked datastore cannot yield a usable capture link, and the raw token
/// is disclosed exactly once to the agent that started the capture.
/// </summary>
public static class SecureCaptureAccessToken
{
    /// <summary>
    /// Creates a new random access token and its storage hash.
    /// </summary>
    /// <returns>A tuple of the raw token to hand to the agent once and the hash to persist.</returns>
    public static (string RawToken, string Hash) Create()
    {
        var rawToken = $"{IdGenerator.GenerateId()}{IdGenerator.GenerateId()}";

        return (rawToken, Hash(rawToken));
    }

    /// <summary>
    /// Computes the storage hash of a raw access token.
    /// </summary>
    /// <param name="rawToken">The raw access token presented by the customer page.</param>
    /// <returns>The lowercase hexadecimal SHA-256 hash, or <see langword="null"/> when the token is empty.</returns>
    public static string Hash(string rawToken)
    {
        if (string.IsNullOrEmpty(rawToken))
        {
            return null;
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));

        return Convert.ToHexStringLower(bytes);
    }
}
