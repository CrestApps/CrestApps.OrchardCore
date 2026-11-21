using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Credentials;
using CrestApps.OrchardCore.Learning.Credentials.Core.OpenBadges;
using CrestApps.OrchardCore.Learning.Credentials.Models;

namespace CrestApps.OrchardCore.Tests.Learning.Credentials;

/// <summary>
/// Phase 8, slice 5b: the credential checked against the Open Badges 3.0 data model (an <c>OpenBadgeCredential</c> is
/// an <c>AchievementCredential</c>) and the W3C Verifiable Credentials Data Model 2.0. The fixtures are written from the
/// specification text and its basic example; no context is fetched.
/// </summary>
public sealed class OpenBadgeCredentialTests
{
    // The shape of the Open Badges 3.0 specification's basic OpenBadgeCredential example, written out by hand: the
    // values are the example's kind of value, not its text.
    private const string SpecificationExample = """
        {
          "@context": [
            "https://www.w3.org/ns/credentials/v2",
            "https://purl.imsglobal.org/spec/ob/v3p0/context-3.0.3.json"
          ],
          "id": "http://example.com/credentials/3527",
          "type": ["VerifiableCredential", "OpenBadgeCredential"],
          "issuer": {
            "id": "https://example.com/issuers/876543",
            "type": ["Profile"],
            "name": "Example Corp"
          },
          "validFrom": "2010-01-01T00:00:00Z",
          "name": "Teamwork Badge",
          "credentialSubject": {
            "type": ["AchievementSubject"],
            "achievement": {
              "id": "https://example.com/achievements/21st-century-skills/teamwork",
              "type": ["Achievement"],
              "criteria": {
                "narrative": "Team members are nominated for this badge by their peers."
              },
              "description": "This badge recognizes the capacity to collaborate within a group.",
              "name": "Teamwork"
            }
          }
        }
        """;

    private const string BaseUrl = "https://lms.example.com/tenant1";

    [Fact]
    public void Build_HasTheShapeOfTheSpecificationExample()
    {
        // Arrange
        var example = JsonNode.Parse(SpecificationExample).AsObject();

        // Act
        var credential = OpenBadgeCredentialBuilder.Build(Input());

        // Assert: every property of the example is there, with the same kind of JSON value.
        AssertSameShape(example, credential, "$");
    }

    [Fact]
    public void Build_StartsWithTheVerifiableCredentialsContext_ThenTheOpenBadgesContext()
    {
        // Act
        var contexts = OpenBadgeCredentialBuilder.Build(Input())["@context"].AsArray().Select(node => node.GetValue<string>()).ToArray();

        // Assert: VC 2.0, section 4.3 (the first item is https://www.w3.org/ns/credentials/v2); OB 3.0 adds its own.
        Assert.Equal(["https://www.w3.org/ns/credentials/v2", "https://purl.imsglobal.org/spec/ob/v3p0/context-3.0.3.json"], contexts);
    }

    [Fact]
    public void Build_IsAVerifiableCredential_AndAnOpenBadgeCredential()
    {
        // Act
        var credential = OpenBadgeCredentialBuilder.Build(Input());

        // Assert
        Assert.Equal(["VerifiableCredential", "OpenBadgeCredential"], Strings(credential["type"]));
        Assert.Equal(["AchievementSubject"], Strings(credential["credentialSubject"]["type"]));
        Assert.Equal(["Achievement"], Strings(credential["credentialSubject"]["achievement"]["type"]));
        Assert.Equal(["Profile"], Strings(credential["issuer"]["type"]));
    }

    [Fact]
    public void Build_HasTheRequiredProperties_WithTheSitesAddresses()
    {
        // Act
        var credential = OpenBadgeCredentialBuilder.Build(Input());

        // Assert: AchievementCredential requires id, type, issuer, validFrom, name and credentialSubject; a Profile
        // requires id and type; an Achievement requires id, type, name, description and criteria.
        Assert.Equal($"{BaseUrl}/learning/credentials/awards/0123456789abcdef0123456789abcdef", credential["id"].GetValue<string>());
        Assert.Equal($"{BaseUrl}/learning/credentials/issuer", credential["issuer"]["id"].GetValue<string>());
        Assert.Equal("Learning tests", credential["issuer"]["name"].GetValue<string>());
        Assert.Equal($"{BaseUrl}/", credential["issuer"]["url"].GetValue<string>());
        Assert.Equal("2026-11-02T12:00:00Z", credential["validFrom"].GetValue<string>());
        Assert.Equal("Forklift operator", credential["name"].GetValue<string>());

        var achievement = credential["credentialSubject"]["achievement"];

        Assert.Equal($"{BaseUrl}/learning/credentials/achievements/def1", achievement["id"].GetValue<string>());
        Assert.Equal("Forklift operator", achievement["name"].GetValue<string>());
        Assert.Equal("Operates a forklift safely.", achievement["description"].GetValue<string>());
        Assert.Equal("Completed the forklift course.", achievement["criteria"]["narrative"].GetValue<string>());
        Assert.True(Uri.IsWellFormedUriString(credential["id"].GetValue<string>(), UriKind.Absolute));
    }

    [Fact]
    public void Build_NamesTheRecipientByAHashedIdentityObject_NeverThePlainAddress()
    {
        // Arrange
        var salt = "a1b2c3";
        var input = Input();
        input.Recipient = new CredentialRecipientIdentity
        {
            IdentityType = "emailAddress",
            IdentityHash = CredentialIdentityHash.Hash("Ana.Lee@Example.com", salt),
            Salt = salt,
        };

        // Act
        var credential = OpenBadgeCredentialBuilder.Build(input);
        var identifier = Assert.Single(credential["credentialSubject"]["identifier"].AsArray());

        // Assert: OB 3.0 IdentityObject (type, hashed, identityHash, identityType, salt); the hash is the algorithm, a
        // $ and the hexadecimal SHA-256 of the identifier with the salt appended, in UTF-8.
        var expected = "sha256$" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("ana.lee@example.com" + salt)));

        Assert.Equal("IdentityObject", identifier["type"].GetValue<string>());
        Assert.True(identifier["hashed"].GetValue<bool>());
        Assert.Equal("emailAddress", identifier["identityType"].GetValue<string>());
        Assert.Equal(salt, identifier["salt"].GetValue<string>());
        Assert.Equal(expected, identifier["identityHash"].GetValue<string>());
        Assert.DoesNotContain("ana.lee@example.com", credential.ToJsonString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Ana.Lee", credential.ToJsonString(), StringComparison.OrdinalIgnoreCase);
        Assert.Null(credential["credentialSubject"]["id"]);
    }

    [Fact]
    public void Build_HasValidUntil_OnlyWhenTheDefinitionSetsAValidity()
    {
        // Arrange
        var expiring = Input();
        expiring.ValidUntilUtc = new DateTime(2027, 11, 2, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var withValidity = OpenBadgeCredentialBuilder.Build(expiring);
        var withoutValidity = OpenBadgeCredentialBuilder.Build(Input());

        // Assert
        Assert.Equal("2027-11-02T12:00:00Z", withValidity["validUntil"].GetValue<string>());
        Assert.False(withoutValidity.ContainsKey("validUntil"));
    }

    [Fact]
    public void Build_NamesTheOpenBadgesSchema()
    {
        // Act
        var schema = Assert.Single(OpenBadgeCredentialBuilder.Build(Input())["credentialSchema"].AsArray());

        // Assert
        Assert.Equal("https://purl.imsglobal.org/spec/ob/v3p0/schema/json/ob_v3p0_achievementcredential_schema.json", schema["id"].GetValue<string>());
        Assert.Equal("1EdTechJsonSchemaValidator2019", schema["type"].GetValue<string>());
    }

    [Fact]
    public void Build_HasABitstringStatusListEntry_ForRevocation()
    {
        // Act
        var status = OpenBadgeCredentialBuilder.Build(Input())["credentialStatus"];

        // Assert: Bitstring Status List, section 2.1: id, type BitstringStatusListEntry, statusPurpose, statusListIndex
        // (an integer as a string) and statusListCredential (the list's address).
        Assert.Equal($"{BaseUrl}/learning/credentials/status/1#94567", status["id"].GetValue<string>());
        Assert.Equal("BitstringStatusListEntry", status["type"].GetValue<string>());
        Assert.Equal("revocation", status["statusPurpose"].GetValue<string>());
        Assert.Equal("94567", status["statusListIndex"].GetValue<string>());
        Assert.Equal($"{BaseUrl}/learning/credentials/status/1", status["statusListCredential"].GetValue<string>());
    }

    [Fact]
    public void Build_CarriesTheAchievementsImageTypeTagsAndAlignments()
    {
        // Arrange
        var input = Input();
        input.Achievement.ImageUrl = $"{BaseUrl}/media/badges/forklift.png";
        input.Achievement.AchievementType = "Certificate";
        input.Achievement.Tags = ["safety", "warehouse"];
        input.Achievement.Alignments =
        [
            new CredentialAlignment { TargetName = "Forklift operation", TargetUrl = "https://framework.example.org/skills/42", TargetType = "ceasn:Competency", TargetCode = "FO-42" },
        ];

        // Act
        var achievement = OpenBadgeCredentialBuilder.Build(input)["credentialSubject"]["achievement"];
        var alignment = Assert.Single(achievement["alignment"].AsArray());

        // Assert
        Assert.Equal($"{BaseUrl}/media/badges/forklift.png", achievement["image"]["id"].GetValue<string>());
        Assert.Equal("Image", achievement["image"]["type"].GetValue<string>());
        Assert.Equal("Certificate", achievement["achievementType"].GetValue<string>());
        Assert.Equal(["safety", "warehouse"], Strings(achievement["tag"]));
        Assert.Equal(["Alignment"], Strings(alignment["type"]));
        Assert.Equal("Forklift operation", alignment["targetName"].GetValue<string>());
        Assert.Equal("https://framework.example.org/skills/42", alignment["targetUrl"].GetValue<string>());
        Assert.Equal("ceasn:Competency", alignment["targetType"].GetValue<string>());
        Assert.Equal("FO-42", alignment["targetCode"].GetValue<string>());
    }

    [Fact]
    public void Build_LeavesOutEmptyOptionalProperties()
    {
        // Act
        var achievement = OpenBadgeCredentialBuilder.Build(Input())["credentialSubject"]["achievement"].AsObject();

        // Assert: an empty array or a null is not a value the schema accepts for an optional property.
        Assert.False(achievement.ContainsKey("image"));
        Assert.False(achievement.ContainsKey("tag"));
        Assert.False(achievement.ContainsKey("alignment"));
        Assert.False(achievement.ContainsKey("achievementType"));
    }

    [Fact]
    public void IdentityHash_Matches_TheAddressTheHolderGives()
    {
        // Arrange
        var salt = CredentialIdentityHash.NewSalt();
        var hash = CredentialIdentityHash.Hash("ana@example.com", salt);

        // Act and assert
        Assert.Equal(32, salt.Length);
        Assert.True(CredentialIdentityHash.Matches(" ANA@example.com ", salt, hash));
        Assert.False(CredentialIdentityHash.Matches("ben@example.com", salt, hash));
        Assert.NotEqual(salt, CredentialIdentityHash.NewSalt());
    }

    private static OpenBadgeCredentialInput Input()
        => new()
        {
            BaseUrl = BaseUrl,
            IssuerName = "Learning tests",
            PublicId = "0123456789abcdef0123456789abcdef",
            Achievement = new CredentialAchievement
            {
                Id = $"{BaseUrl}/learning/credentials/achievements/def1",
                Name = "Forklift operator",
                Description = "Operates a forklift safely.",
                Criteria = "Completed the forklift course.",
            },
            Recipient = new CredentialRecipientIdentity
            {
                IdentityType = "emailAddress",
                IdentityHash = CredentialIdentityHash.Hash("ana@example.com", "salt"),
                Salt = "salt",
            },
            ValidFromUtc = new DateTime(2026, 11, 2, 12, 0, 0, 123, DateTimeKind.Utc),
            StatusListNumber = 1,
            StatusListIndex = 94567,
        };

    private static string[] Strings(JsonNode node)
        => node.AsArray().Select(item => item.GetValue<string>()).ToArray();

    private static void AssertSameShape(JsonNode expected, JsonNode actual, string path)
    {
        Assert.True(actual is not null, $"{path} is missing.");
        Assert.True(expected.GetValueKind() == actual.GetValueKind(), $"{path} is a {actual.GetValueKind()}, the example has a {expected.GetValueKind()}.");

        if (expected is JsonObject expectedObject)
        {
            foreach (var (name, value) in expectedObject)
            {
                AssertSameShape(value, actual[name], $"{path}.{name}");
            }
        }
    }
}
