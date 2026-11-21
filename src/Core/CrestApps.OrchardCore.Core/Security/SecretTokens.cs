using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace CrestApps.OrchardCore.Core.Security;

/// <summary>
/// The one place a secret token is made: the random value in a feed address, an API key, a one-time link or a capture
/// page, which is shown once (or kept protected) and stored only as its hash. Never call
/// <see cref="RandomNumberGenerator"/> and <see cref="SHA256"/> by hand for a token; a signed, time-limited value goes
/// through <c>SignedTokenService</c> instead.
/// </summary>
public static class SecretTokens
{
    /// <summary>
    /// The number of random bytes in a token unless a caller asks for another size: 256 bits.
    /// </summary>
    public const int DefaultByteLength = 32;

    /// <summary>
    /// Returns a new token: <paramref name="bytes"/> bytes from <see cref="RandomNumberGenerator"/>, base64url encoded
    /// without padding (43 characters for the default 32 bytes), safe in a URL path, a query string or a header.
    /// </summary>
    /// <param name="bytes">How many random bytes the token carries; at least 16.</param>
    public static string Create(int bytes = DefaultByteLength)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bytes, 16);

        return WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(bytes));
    }

    /// <summary>
    /// Returns the SHA-256 of a token as lower-case hex, the form new stores keep and look a token up by. A store that
    /// already keeps another form keeps it.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <returns>The hash, or <see langword="null"/> for an empty token.</returns>
    public static string Hash(string token)
        => string.IsNullOrEmpty(token) ? null : Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>
    /// Returns whether two strings are equal, in a time that doesn't depend on where they differ, so a secret can't be
    /// guessed a character at a time.
    /// </summary>
    /// <param name="left">One value.</param>
    /// <param name="right">The other value.</param>
    public static bool FixedTimeEquals(string left, string right)
    {
        if (left is null || right is null)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));
    }
}
