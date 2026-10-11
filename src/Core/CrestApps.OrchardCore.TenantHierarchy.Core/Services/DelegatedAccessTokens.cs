using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Services;

/// <summary>
/// Creates and checks the random values of delegated access: one-time codes, session identifiers, state values and
/// PKCE verifiers and challenges.
/// </summary>
public static class DelegatedAccessTokens
{
    /// <summary>
    /// Creates a random value of 256 bits, encoded as base64url.
    /// </summary>
    public static string CreateToken()
    {
        return WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    }

    /// <summary>
    /// Returns the SHA-256 hash of a value, encoded as base64url. Codes and session identifiers are stored as hashes.
    /// </summary>
    /// <param name="value">The value.</param>
    public static string Hash(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    /// <summary>
    /// Returns the PKCE <c>S256</c> code challenge of a verifier.
    /// </summary>
    /// <param name="codeVerifier">The verifier.</param>
    public static string CreateCodeChallenge(string codeVerifier)
    {
        ArgumentException.ThrowIfNullOrEmpty(codeVerifier);

        return WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));
    }

    /// <summary>
    /// Returns whether a verifier matches a code challenge, in constant time.
    /// </summary>
    /// <param name="codeVerifier">The verifier.</param>
    /// <param name="codeChallenge">The challenge.</param>
    public static bool VerifyCodeChallenge(string codeVerifier, string codeChallenge)
    {
        if (string.IsNullOrEmpty(codeVerifier) || string.IsNullOrEmpty(codeChallenge))
        {
            return false;
        }

        var expected = Encoding.ASCII.GetBytes(CreateCodeChallenge(codeVerifier));
        var actual = Encoding.ASCII.GetBytes(codeChallenge);

        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    /// <summary>
    /// Returns a short reference to a session hash for logs and the activity log. It cannot be used to find the session.
    /// </summary>
    /// <param name="sessionHash">The session hash.</param>
    public static string GetReference(string sessionHash)
    {
        if (string.IsNullOrEmpty(sessionHash))
        {
            return null;
        }

        return sessionHash.Length > 8
            ? sessionHash.Substring(0, 8)
            : sessionHash;
    }

    /// <summary>
    /// Returns the version of a set of roles. The child renews its roles when the version changes.
    /// </summary>
    /// <param name="roles">The roles.</param>
    public static string ComputeRolesVersion(IEnumerable<string> roles)
    {
        var normalized = string.Join('\n', (roles ?? []).Select(role => role.ToUpperInvariant()).Distinct().Order(StringComparer.Ordinal));

        return Hash(normalized).Substring(0, 16);
    }
}
