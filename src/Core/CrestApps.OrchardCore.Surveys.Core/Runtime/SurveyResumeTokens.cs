using System.Security.Cryptography;
using CrestApps.OrchardCore.Surveys.Core.Access;

namespace CrestApps.OrchardCore.Surveys.Core.Runtime;

/// <summary>
/// Issues and checks resume tokens. A token is <c>{responseId}.{secret}</c> with a 256-bit secret, so it finds its
/// response; only its SHA-256 hash is stored.
/// </summary>
public static class SurveyResumeTokens
{
    /// <summary>
    /// Issues a token for a response.
    /// </summary>
    /// <param name="responseId">The response id.</param>
    /// <returns>The token.</returns>
    public static string Issue(string responseId)
    {
        ArgumentException.ThrowIfNullOrEmpty(responseId);

        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        return responseId + "." + secret;
    }

    /// <summary>
    /// Gets the response id a token belongs to.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <returns>The response id, or <see langword="null"/>.</returns>
    public static string ResponseIdOf(string token)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 200)
        {
            return null;
        }

        var dot = token.IndexOf('.', StringComparison.Ordinal);

        return dot > 0 ? token.Substring(0, dot) : null;
    }

    /// <summary>
    /// Gets the hash stored for a token.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <returns>The hash.</returns>
    public static string Hash(string token)
        => SurveyRespondentKeys.Hash(token);

    /// <summary>
    /// Checks a token against a stored hash in constant time.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <param name="hash">The stored hash.</param>
    /// <returns><see langword="true"/> when it matches.</returns>
    public static bool Matches(string token, string hash)
    {
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(hash))
        {
            return false;
        }

        var computed = System.Text.Encoding.ASCII.GetBytes(Hash(token));
        var stored = System.Text.Encoding.ASCII.GetBytes(hash);

        return CryptographicOperations.FixedTimeEquals(computed, stored);
    }
}
