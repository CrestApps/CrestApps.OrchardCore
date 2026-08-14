using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// Generates PKCE (Proof Key for Code Exchange) values for the OAuth 2.0 authorization code flow as
/// defined in RFC 7636.
/// </summary>
public static class TelephonyPkceGenerator
{
    /// <summary>
    /// The S256 code challenge method.
    /// </summary>
    public const string Sha256Method = "S256";

    /// <summary>
    /// Creates a high-entropy cryptographic code verifier.
    /// </summary>
    /// <returns>A base64url-encoded code verifier.</returns>
    public static string CreateCodeVerifier()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);

        return WebEncoders.Base64UrlEncode(bytes);
    }

    /// <summary>
    /// Derives the S256 code challenge for the given code verifier.
    /// </summary>
    /// <param name="codeVerifier">The code verifier the challenge is derived from.</param>
    /// <returns>The base64url-encoded SHA-256 code challenge.</returns>
    public static string CreateCodeChallenge(string codeVerifier)
    {
        ArgumentException.ThrowIfNullOrEmpty(codeVerifier);

        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));

        return WebEncoders.Base64UrlEncode(hash);
    }
}
