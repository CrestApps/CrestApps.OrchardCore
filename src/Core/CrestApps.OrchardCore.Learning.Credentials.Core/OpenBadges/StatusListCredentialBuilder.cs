using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.Learning.Credentials.Core.OpenBadges;

/// <summary>
/// Builds a W3C <c>BitstringStatusListCredential</c> whose purpose is revocation.
/// </summary>
public static class StatusListCredentialBuilder
{
    /// <summary>
    /// Builds the status list credential, without a proof.
    /// </summary>
    /// <param name="baseUrl">The site's base address, without a trailing slash.</param>
    /// <param name="listNumber">The number of the list.</param>
    /// <param name="encodedList">The encoded list.</param>
    /// <param name="validFromUtc">When the list was made, in UTC.</param>
    /// <returns>The credential.</returns>
    public static JsonObject Build(string baseUrl, int listNumber, string encodedList, DateTime validFromUtc)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseUrl);
        ArgumentException.ThrowIfNullOrEmpty(encodedList);

        var id = CredentialUrls.StatusList(baseUrl, listNumber);

        return new JsonObject
        {
            ["@context"] = new JsonArray(CredentialsConstants.OpenBadges.VerifiableCredentialsContext),
            ["id"] = id,
            ["type"] = new JsonArray("VerifiableCredential", CredentialsConstants.OpenBadges.StatusListCredentialType),
            ["issuer"] = CredentialUrls.Issuer(baseUrl),
            ["validFrom"] = OpenBadgeCredentialBuilder.DateTime(validFromUtc),
            ["credentialSubject"] = new JsonObject
            {
                ["id"] = id + "#list",
                ["type"] = CredentialsConstants.OpenBadges.StatusListType,
                ["statusPurpose"] = CredentialsConstants.OpenBadges.RevocationPurpose,
                ["encodedList"] = encodedList,
            },
        };
    }
}
