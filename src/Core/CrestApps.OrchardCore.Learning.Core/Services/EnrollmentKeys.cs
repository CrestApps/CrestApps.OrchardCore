using System.Security.Cryptography;
using System.Text;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// Hashes and checks course enrollment keys. A key is never stored: only a salted PBKDF2 hash, compared in constant time.
/// </summary>
public static class EnrollmentKeys
{
    private const string Version = "v1";
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    /// <summary>
    /// Hashes a key.
    /// </summary>
    /// <param name="key">The key, trimmed before hashing.</param>
    /// <returns>The hash, with its version and salt.</returns>
    public static string Hash(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Derive(key.Trim(), salt);

        return $"{Version}:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// Checks a key against a hash.
    /// </summary>
    /// <param name="key">The key the learner typed.</param>
    /// <param name="hash">The stored hash.</param>
    /// <returns><see langword="true"/> when the key matches.</returns>
    public static bool Verify(string key, string hash)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrEmpty(hash))
        {
            return false;
        }

        var parts = hash.Split(':');

        if (parts.Length != 3 || parts[0] != Version)
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[1]);
            var expected = Convert.FromBase64String(parts[2]);

            return CryptographicOperations.FixedTimeEquals(Derive(key.Trim(), salt), expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static byte[] Derive(string key, byte[] salt)
        => Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(key), salt, Iterations, HashAlgorithmName.SHA256, HashSize);
}
