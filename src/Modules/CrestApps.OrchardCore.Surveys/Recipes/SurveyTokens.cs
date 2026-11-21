using System.Security.Cryptography;

namespace CrestApps.OrchardCore.Surveys.Recipes;

/// <summary>
/// Makes the random values Surveys gives records that are imported without them.
/// </summary>
internal static class SurveyTokens
{
    /// <summary>
    /// Makes a 22-character URL-safe random token for a collector link.
    /// </summary>
    /// <returns>The token.</returns>
    public static string NewCollectorToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(16))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

    /// <summary>
    /// Makes a new anonymity secret: 32 random bytes in base64.
    /// </summary>
    /// <returns>The secret.</returns>
    public static string NewAnonymitySecret()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
}
