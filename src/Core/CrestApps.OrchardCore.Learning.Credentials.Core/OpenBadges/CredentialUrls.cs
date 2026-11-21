namespace CrestApps.OrchardCore.Learning.Credentials.Core.OpenBadges;

/// <summary>
/// The public addresses of Digital Credentials, under the site's base address (with the tenant prefix).
/// </summary>
public static class CredentialUrls
{
    /// <summary>
    /// The path every public address starts with.
    /// </summary>
    public const string Root = "learning/credentials";

    /// <summary>
    /// The address of a credential in JSON-LD, which is the credential's <c>id</c>.
    /// </summary>
    /// <param name="baseUrl">The site's base address, without a trailing slash.</param>
    /// <param name="publicId">The award's public code.</param>
    /// <returns>The address.</returns>
    public static string Credential(string baseUrl, string publicId)
        => $"{baseUrl}/{Root}/awards/{publicId}";

    /// <summary>
    /// The address of a credential signed as a JSON Web Token.
    /// </summary>
    /// <param name="baseUrl">The site's base address, without a trailing slash.</param>
    /// <param name="publicId">The award's public code.</param>
    /// <returns>The address.</returns>
    public static string CredentialJwt(string baseUrl, string publicId)
        => $"{Credential(baseUrl, publicId)}/jwt";

    /// <summary>
    /// The address of the human verification page, which the learner shares.
    /// </summary>
    /// <param name="baseUrl">The site's base address, without a trailing slash.</param>
    /// <param name="publicId">The award's public code.</param>
    /// <returns>The address.</returns>
    public static string Verify(string baseUrl, string publicId)
        => $"{baseUrl}/{Root}/verify/{publicId}";

    /// <summary>
    /// The address of an achievement, which is the achievement's <c>id</c>.
    /// </summary>
    /// <param name="baseUrl">The site's base address, without a trailing slash.</param>
    /// <param name="definitionId">The content item id of the credential definition.</param>
    /// <returns>The address.</returns>
    public static string Achievement(string baseUrl, string definitionId)
        => $"{baseUrl}/{Root}/achievements/{definitionId}";

    /// <summary>
    /// The address of the issuer profile, which is the issuer's <c>id</c>.
    /// </summary>
    /// <param name="baseUrl">The site's base address, without a trailing slash.</param>
    /// <returns>The address.</returns>
    public static string Issuer(string baseUrl)
        => $"{baseUrl}/{Root}/issuer";

    /// <summary>
    /// The address of the published key set.
    /// </summary>
    /// <param name="baseUrl">The site's base address, without a trailing slash.</param>
    /// <returns>The address.</returns>
    public static string KeySet(string baseUrl)
        => $"{baseUrl}/{Root}/jwks";

    /// <summary>
    /// The address of one public key as a JSON Web Key, which is the <c>kid</c> of the tokens it signs.
    /// </summary>
    /// <param name="baseUrl">The site's base address, without a trailing slash.</param>
    /// <param name="kid">The key id in the key set.</param>
    /// <returns>The address.</returns>
    public static string Key(string baseUrl, string kid)
        => $"{baseUrl}/{Root}/keys/{kid}";

    /// <summary>
    /// The address of a status list credential.
    /// </summary>
    /// <param name="baseUrl">The site's base address, without a trailing slash.</param>
    /// <param name="listNumber">The number of the list.</param>
    /// <returns>The address.</returns>
    public static string StatusList(string baseUrl, int listNumber)
        => $"{baseUrl}/{Root}/status/{listNumber}";

    /// <summary>
    /// Reads the key id from the address of a key, or returns the value when it is not such an address.
    /// </summary>
    /// <param name="kid">The <c>kid</c> of a token.</param>
    /// <returns>The key id in the key set.</returns>
    public static string KeyIdOf(string kid)
    {
        if (string.IsNullOrEmpty(kid))
        {
            return kid;
        }

        var marker = $"/{Root}/keys/";
        var index = kid.LastIndexOf(marker, StringComparison.Ordinal);

        return index < 0 ? kid : kid.Substring(index + marker.Length);
    }
}
