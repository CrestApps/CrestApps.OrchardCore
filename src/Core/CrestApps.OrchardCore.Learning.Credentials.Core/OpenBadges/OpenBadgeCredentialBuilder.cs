using System.Globalization;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Credentials.Models;

namespace CrestApps.OrchardCore.Learning.Credentials.Core.OpenBadges;

/// <summary>
/// Builds an Open Badges 3.0 <c>OpenBadgeCredential</c> on the W3C Verifiable Credentials Data Model 2.0.
/// </summary>
public static class OpenBadgeCredentialBuilder
{
    /// <summary>
    /// Builds the credential, without a proof.
    /// </summary>
    /// <param name="input">What it is built from.</param>
    /// <returns>The credential.</returns>
    public static JsonObject Build(OpenBadgeCredentialInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrEmpty(input.BaseUrl);
        ArgumentException.ThrowIfNullOrEmpty(input.PublicId);
        ArgumentNullException.ThrowIfNull(input.Achievement);
        ArgumentNullException.ThrowIfNull(input.Recipient);

        var statusList = CredentialUrls.StatusList(input.BaseUrl, input.StatusListNumber);
        var index = input.StatusListIndex.ToString(CultureInfo.InvariantCulture);
        var credential = new JsonObject
        {
            ["@context"] = new JsonArray(CredentialsConstants.OpenBadges.VerifiableCredentialsContext, CredentialsConstants.OpenBadges.OpenBadgesContext),
            ["id"] = CredentialUrls.Credential(input.BaseUrl, input.PublicId),
            ["type"] = new JsonArray("VerifiableCredential", "OpenBadgeCredential"),
            ["issuer"] = Issuer(input.BaseUrl, input.IssuerName),
            ["validFrom"] = DateTime(input.ValidFromUtc),
        };

        if (input.ValidUntilUtc is { } validUntil)
        {
            credential["validUntil"] = DateTime(validUntil);
        }

        credential["name"] = input.Achievement.Name;
        credential["awardedDate"] = DateTime(input.ValidFromUtc);
        credential["credentialSubject"] = new JsonObject
        {
            ["type"] = new JsonArray("AchievementSubject"),
            ["identifier"] = new JsonArray(new JsonObject
            {
                ["type"] = "IdentityObject",
                ["hashed"] = true,
                ["identityHash"] = input.Recipient.IdentityHash,
                ["identityType"] = string.IsNullOrEmpty(input.Recipient.IdentityType) ? CredentialIdentityHash.EmailAddress : input.Recipient.IdentityType,
                ["salt"] = input.Recipient.Salt,
            }),
            ["achievement"] = Achievement(input.Achievement),
        };
        credential["credentialSchema"] = new JsonArray(new JsonObject
        {
            ["id"] = CredentialsConstants.OpenBadges.CredentialSchema,
            ["type"] = CredentialsConstants.OpenBadges.CredentialSchemaType,
        });
        credential["credentialStatus"] = new JsonObject
        {
            ["id"] = $"{statusList}#{index}",
            ["type"] = CredentialsConstants.OpenBadges.StatusEntryType,
            ["statusPurpose"] = CredentialsConstants.OpenBadges.RevocationPurpose,
            ["statusListIndex"] = index,
            ["statusListCredential"] = statusList,
        };

        return credential;
    }

    /// <summary>
    /// Builds the Open Badges 3.0 issuer profile.
    /// </summary>
    /// <param name="baseUrl">The site's base address, without a trailing slash.</param>
    /// <param name="name">The issuer's name.</param>
    /// <returns>The profile.</returns>
    public static JsonObject Issuer(string baseUrl, string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseUrl);

        return new JsonObject
        {
            ["id"] = CredentialUrls.Issuer(baseUrl),
            ["type"] = new JsonArray("Profile"),
            ["name"] = string.IsNullOrWhiteSpace(name) ? baseUrl : name,
            ["url"] = baseUrl + "/",
        };
    }

    /// <summary>
    /// Builds the Open Badges 3.0 achievement.
    /// </summary>
    /// <param name="achievement">The achievement, with its address.</param>
    /// <returns>The achievement.</returns>
    public static JsonObject Achievement(CredentialAchievement achievement)
    {
        ArgumentNullException.ThrowIfNull(achievement);

        var description = FirstText(achievement.Description, achievement.Name);
        var result = new JsonObject
        {
            ["id"] = achievement.Id,
            ["type"] = new JsonArray("Achievement"),
            ["name"] = achievement.Name,
            ["description"] = description,
            ["criteria"] = new JsonObject
            {
                ["narrative"] = FirstText(achievement.Criteria, description),
            },
        };

        if (!string.IsNullOrWhiteSpace(achievement.ImageUrl))
        {
            result["image"] = new JsonObject
            {
                ["id"] = achievement.ImageUrl,
                ["type"] = "Image",
            };
        }

        if (!string.IsNullOrWhiteSpace(achievement.AchievementType))
        {
            result["achievementType"] = achievement.AchievementType;
        }

        var tags = (achievement.Tags ?? []).Where(tag => !string.IsNullOrWhiteSpace(tag)).ToArray();

        if (tags.Length > 0)
        {
            result["tag"] = new JsonArray(tags.Select(tag => (JsonNode)tag).ToArray());
        }

        var alignments = (achievement.Alignments ?? [])
            .Where(alignment => alignment is not null && !string.IsNullOrWhiteSpace(alignment.TargetName) && !string.IsNullOrWhiteSpace(alignment.TargetUrl))
            .Select(Alignment)
            .ToArray();

        if (alignments.Length > 0)
        {
            result["alignment"] = new JsonArray(alignments);
        }

        return result;
    }

    /// <summary>
    /// Formats a UTC moment as the data model's date-time, to the second with a <c>Z</c>.
    /// </summary>
    /// <param name="utc">The moment.</param>
    /// <returns>The text.</returns>
    public static string DateTime(System.DateTime utc)
        => System.DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static JsonNode Alignment(CredentialAlignment alignment)
    {
        var result = new JsonObject
        {
            ["type"] = new JsonArray("Alignment"),
            ["targetName"] = alignment.TargetName.Trim(),
            ["targetUrl"] = alignment.TargetUrl.Trim(),
        };

        Add(result, "targetDescription", alignment.TargetDescription);
        Add(result, "targetFramework", alignment.TargetFramework);
        Add(result, "targetCode", alignment.TargetCode);
        Add(result, "targetType", alignment.TargetType);

        return result;
    }

    private static void Add(JsonObject target, string name, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            target[name] = value.Trim();
        }
    }

    private static string FirstText(string first, string second)
        => string.IsNullOrWhiteSpace(first) ? second ?? string.Empty : first;
}
