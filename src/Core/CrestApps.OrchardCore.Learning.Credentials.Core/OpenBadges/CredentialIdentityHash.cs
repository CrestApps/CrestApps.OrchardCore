using System.Security.Cryptography;
using System.Text;

namespace CrestApps.OrchardCore.Learning.Credentials.Core.OpenBadges;

/// <summary>
/// The Open Badges 3.0 identity hash: an algorithm name, a <c>$</c> and the hexadecimal hash of the identifier with the
/// salt appended, both in UTF-8. The e-mail address is trimmed and lowercased first, so the holder can prove it.
/// </summary>
public static class CredentialIdentityHash
{
    /// <summary>
    /// The identity type of an e-mail address.
    /// </summary>
    public const string EmailAddress = "emailAddress";

    private const string _prefix = "sha256$";

    /// <summary>
    /// Hashes an e-mail address with a salt.
    /// </summary>
    /// <param name="email">The e-mail address.</param>
    /// <param name="salt">The salt.</param>
    /// <returns><c>sha256$</c> and the lowercase hexadecimal SHA-256.</returns>
    public static string Hash(string email, string salt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var identifier = email.Trim().ToLowerInvariant() + (salt ?? string.Empty);

        return _prefix + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(identifier)));
    }

    /// <summary>
    /// Makes a new random salt (128 bits, hexadecimal).
    /// </summary>
    /// <returns>The salt.</returns>
    public static string NewSalt()
        => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));

    /// <summary>
    /// Tells whether an e-mail address matches a hash.
    /// </summary>
    /// <param name="email">The e-mail address.</param>
    /// <param name="salt">The salt.</param>
    /// <param name="identityHash">The hash.</param>
    /// <returns><see langword="true"/> when it matches.</returns>
    public static bool Matches(string email, string salt, string identityHash)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(identityHash))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(Hash(email, salt)),
            Encoding.ASCII.GetBytes(identityHash.ToLowerInvariant()));
    }
}
