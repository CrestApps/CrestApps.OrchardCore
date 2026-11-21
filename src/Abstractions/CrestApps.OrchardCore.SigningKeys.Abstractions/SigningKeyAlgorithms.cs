namespace CrestApps.OrchardCore.SigningKeys;

/// <summary>
/// The JSON Web Signature algorithms a key set can sign with.
/// </summary>
public static class SigningKeyAlgorithms
{
    /// <summary>
    /// RSASSA-PKCS1-v1_5 with SHA-256 on a 2048-bit RSA key, kept as an Orchard Core <c>RsaKeySecret</c>.
    /// </summary>
    public const string RS256 = "RS256";

    /// <summary>
    /// ECDSA on the P-256 curve with SHA-256, kept as an Orchard Core <c>TextSecret</c> holding the private JSON Web Key,
    /// because Orchard Core Secrets has no elliptic curve key type.
    /// </summary>
    public const string ES256 = "ES256";

    /// <summary>
    /// Gets whether a key set can use an algorithm.
    /// </summary>
    /// <param name="algorithm">The algorithm.</param>
    /// <returns><see langword="true"/> for <see cref="RS256"/> and <see cref="ES256"/>.</returns>
    public static bool IsSupported(string algorithm)
        => algorithm is RS256 or ES256;
}
