using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Credentials.Core.OpenBadges;
using CrestApps.OrchardCore.Learning.Credentials.Models;
using CrestApps.OrchardCore.SigningKeys.Core;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CrestApps.OrchardCore.Tests.Learning.Credentials;

/// <summary>
/// Phase 8, slice 5b: the JSON Web Token proof format of Open Badges 3.0 (section 8.2): the JOSE header (alg RS256, typ
/// JWT, kid the address of the public key), the payload (the credential with iss, jti, nbf and, with validUntil, exp)
/// and an RS256 signature any JOSE library checks.
/// </summary>
public sealed class CredentialJwsTests
{
    private const string BaseUrl = "https://lms.example.com";
    private const string KeyUrl = BaseUrl + "/learning/credentials/keys/credentials-1";

    private static readonly JsonObject _privateJwk = SigningKeyJwk.Create("RS256", "credentials-1");

    [Fact]
    public void Sign_TheHeader_IsRs256_TypeJwt_AndTheKidIsTheAddressOfTheKey()
    {
        // Act
        var token = CredentialJws.Sign(CredentialJws.Payload(Credential(expires: false)), _privateJwk, KeyUrl);
        var header = JsonNode.Parse(Base64Url.DecodeFromChars(token.Split('.')[0])).AsObject();

        // Assert
        Assert.Equal("RS256", header["alg"].GetValue<string>());
        Assert.Equal("JWT", header["typ"].GetValue<string>());
        Assert.Equal(KeyUrl, header["kid"].GetValue<string>());
        Assert.Equal(3, header.Count);
    }

    [Fact]
    public void Payload_IsTheCredential_WithTheRegisteredClaims()
    {
        // Arrange
        var credential = Credential(expires: true);

        // Act
        var payload = CredentialJws.Payload(credential);

        // Assert: iss is the issuer's id, jti the credential's id, nbf validFrom and exp validUntil as NumericDates.
        Assert.Equal(credential["issuer"]["id"].GetValue<string>(), payload["iss"].GetValue<string>());
        Assert.Equal(credential["id"].GetValue<string>(), payload["jti"].GetValue<string>());
        Assert.Equal(new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(), payload["nbf"].GetValue<long>());
        Assert.Equal(new DateTimeOffset(2027, 11, 2, 12, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(), payload["exp"].GetValue<long>());
        Assert.False(payload.ContainsKey("sub"));

        foreach (var (name, value) in credential)
        {
            Assert.True(JsonNode.DeepEquals(value, payload[name]), $"The payload's {name} differs from the credential's.");
        }
    }

    [Fact]
    public void Payload_HasNoExp_WithoutValidUntil()
    {
        // Act
        var payload = CredentialJws.Payload(Credential(expires: false));

        // Assert
        Assert.False(payload.ContainsKey("exp"));
    }

    [Fact]
    public void Sign_TheSignature_IsRs256OverTheSigningInput()
    {
        // Arrange
        var token = CredentialJws.Sign(CredentialJws.Payload(Credential(expires: false)), _privateJwk, KeyUrl);
        var parts = token.Split('.');

        using var rsa = RSA.Create(new RSAParameters
        {
            Modulus = Base64Url.DecodeFromChars(_privateJwk["n"].GetValue<string>()),
            Exponent = Base64Url.DecodeFromChars(_privateJwk["e"].GetValue<string>()),
        });

        // Act: checked here with the platform's RSA, not with the code under test.
        var valid = rsa.VerifyData(Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]), Base64Url.DecodeFromChars(parts[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        // Assert
        Assert.Equal(3, parts.Length);
        Assert.True(valid);
    }

    [Fact]
    public async Task Sign_AJoseLibrary_ValidatesTheToken()
    {
        // Arrange
        var credential = Credential(expires: true);
        var token = CredentialJws.Sign(CredentialJws.Payload(credential), _privateJwk, KeyUrl);
        var publicKey = new JsonWebKey(SigningKeyJwk.PublicOf(_privateJwk).ToJsonString());

        // Act
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = credential["issuer"]["id"].GetValue<string>(),
            ValidateAudience = false,
            ValidateLifetime = false,
            IssuerSigningKey = publicKey,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
        });

        // Assert
        Assert.True(result.IsValid, result.Exception?.Message);
        Assert.Equal(credential["id"].GetValue<string>(), ((JsonWebToken)result.SecurityToken).Id);
    }

    [Fact]
    public void Verify_AcceptsTheKeyThatSigned_AndRefusesAnotherKeyOrAChangedPayload()
    {
        // Arrange
        var token = CredentialJws.Sign(CredentialJws.Payload(Credential(expires: false)), _privateJwk, KeyUrl);
        var otherKey = SigningKeyJwk.PublicOf(SigningKeyJwk.Create("RS256", "other"));
        var parts = token.Split('.');
        var changed = JsonNode.Parse(Base64Url.DecodeFromChars(parts[1])).AsObject();
        changed["name"] = "Forged";
        var tampered = parts[0] + "." + Base64Url.EncodeToString(Encoding.UTF8.GetBytes(changed.ToJsonString())) + "." + parts[2];

        // Act and assert
        Assert.True(CredentialJws.Verify(token, SigningKeyJwk.PublicOf(_privateJwk)));
        Assert.False(CredentialJws.Verify(token, otherKey));
        Assert.False(CredentialJws.Verify(tampered, SigningKeyJwk.PublicOf(_privateJwk)));
        Assert.False(CredentialJws.Verify("not.a-token", SigningKeyJwk.PublicOf(_privateJwk)));
    }

    [Fact]
    public void Read_ReturnsTheHeaderAndThePayload()
    {
        // Arrange
        var payload = CredentialJws.Payload(Credential(expires: false));
        var token = CredentialJws.Sign(payload, _privateJwk, KeyUrl);

        // Act
        var (header, read) = CredentialJws.Read(token);
        var (badHeader, badPayload) = CredentialJws.Read("garbage");

        // Assert
        Assert.Equal(KeyUrl, header["kid"].GetValue<string>());
        Assert.True(JsonNode.DeepEquals(payload, read));
        Assert.Null(badHeader);
        Assert.Null(badPayload);
    }

    [Fact]
    public void KeyIdOf_ReadsTheKeyIdFromTheAddressOfTheKey()
    {
        // Act and assert
        Assert.Equal("credentials-1", CredentialUrls.KeyIdOf(KeyUrl));
        Assert.Equal("credentials-1", CredentialUrls.KeyIdOf("credentials-1"));
    }

    private static JsonObject Credential(bool expires)
        => OpenBadgeCredentialBuilder.Build(new OpenBadgeCredentialInput
        {
            BaseUrl = BaseUrl,
            IssuerName = "Learning tests",
            PublicId = "0123456789abcdef0123456789abcdef",
            Achievement = new CredentialAchievement
            {
                Id = BaseUrl + "/learning/credentials/achievements/def1",
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
            ValidFromUtc = new DateTime(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc),
            ValidUntilUtc = expires ? new DateTime(2027, 11, 2, 12, 0, 0, DateTimeKind.Utc) : null,
            StatusListNumber = 1,
            StatusListIndex = 7,
        });
}
