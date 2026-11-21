using System.Buffers.Text;
using System.Text;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Credentials;
using CrestApps.OrchardCore.Learning.Credentials.Core.OpenBadges;
using CrestApps.OrchardCore.Learning.Credentials.Core.Services;
using CrestApps.OrchardCore.Learning.Credentials.Models;
using CrestApps.OrchardCore.Learning.Credentials.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Learning.Credentials;

/// <summary>
/// Phase 8, slice 5c: the status list credential (W3C Bitstring Status List, section 2.2) and the server-side check the
/// verification page makes: the signature against the published key set, the expiry and the revocation bit read from
/// the status list.
/// </summary>
public sealed class CredentialVerificationTests
{
    [Fact]
    public void StatusListCredential_HasTheShapeOfTheSpecificationExample()
    {
        // Arrange: the specification's example BitstringStatusListCredential, written out by hand.
        var example = JsonNode.Parse("""
            {
              "@context": ["https://www.w3.org/ns/credentials/v2"],
              "id": "https://example.com/credentials/status/3",
              "type": ["VerifiableCredential", "BitstringStatusListCredential"],
              "issuer": "did:example:12345",
              "validFrom": "2021-04-05T14:27:40Z",
              "credentialSubject": {
                "id": "https://example.com/status/3#list",
                "type": "BitstringStatusList",
                "statusPurpose": "revocation",
                "encodedList": "uH4sIAAAAAAAAA-3BMQEAAADCoPVPbQwfoAAAAAAAAAAAAAAAAAAAAIC3AYbSVKsAQAAA"
              }
            }
            """).AsObject();

        // Act
        var credential = StatusListCredentialBuilder.Build(CredentialTestHarness.BaseUrl, 3, "uENCODED", new DateTime(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc));

        // Assert
        foreach (var (name, value) in example)
        {
            Assert.True(credential[name] is not null, $"{name} is missing.");
            Assert.Equal(value.GetValueKind(), credential[name].GetValueKind());
        }

        Assert.Equal(["VerifiableCredential", "BitstringStatusListCredential"], credential["type"].AsArray().Select(node => node.GetValue<string>()));
        Assert.Equal(CredentialUrls.StatusList(CredentialTestHarness.BaseUrl, 3), credential["id"].GetValue<string>());
        Assert.Equal(CredentialUrls.Issuer(CredentialTestHarness.BaseUrl), credential["issuer"].GetValue<string>());
        Assert.Equal(CredentialUrls.StatusList(CredentialTestHarness.BaseUrl, 3) + "#list", credential["credentialSubject"]["id"].GetValue<string>());
        Assert.Equal("BitstringStatusList", credential["credentialSubject"]["type"].GetValue<string>());
        Assert.Equal("revocation", credential["credentialSubject"]["statusPurpose"].GetValue<string>());
        Assert.Equal("uENCODED", credential["credentialSubject"]["encodedList"].GetValue<string>());
        Assert.Equal("2026-11-02T12:00:00Z", credential["validFrom"].GetValue<string>());
    }

    [Fact]
    public async Task ARevocation_ShowsInTheStatusListAtOnce_AndTheSlotStaysTheAwards()
    {
        // Arrange
        await using var h = await CredentialTestHarness.CreateAsync();
        h.Definitions.Add("forklift");
        var ana = await ManualAsync(h, "ana");
        var ben = await ManualAsync(h, "ben");

        // Act
        var before = await h.RunAsync(services => services.GetRequiredService<CredentialStatusLists>().EncodeAsync(ana.StatusListNumber));
        await h.RunAsync(services => services.GetRequiredService<ICredentialAwardService>().RevokeAsync(ana.ItemId, "admin", "Cheating"));
        var after = await h.RunAsync(services => services.GetRequiredService<CredentialStatusLists>().EncodeAsync(ana.StatusListNumber));
        var cleo = await ManualAsync(h, "cleo");

        // Assert
        Assert.False(BitstringStatusList.IsSet(before, ana.StatusListIndex));
        Assert.True(BitstringStatusList.IsSet(after, ana.StatusListIndex));
        Assert.False(BitstringStatusList.IsSet(after, ben.StatusListIndex));
        Assert.NotEqual(ana.StatusListIndex, cleo.StatusListIndex);
    }

    [Fact]
    public async Task Verify_AValidAward_IsValid()
    {
        // Arrange
        await using var h = await CredentialTestHarness.CreateAsync();
        h.Definitions.Add("forklift").Achievement.ValidityMonths = 12;
        var award = await ManualAsync(h, "ana");

        // Act
        var result = await VerifyAsync(h, award.ItemId);

        // Assert
        Assert.Equal(CredentialVerificationOutcome.Valid, result.Outcome);
        Assert.True(result.SignatureValid);
        Assert.False(result.Revoked);
        Assert.False(result.Expired);
        Assert.Equal(LearningTestHarness.Now.AddMonths(12), result.ValidUntilUtc);
    }

    [Fact]
    public async Task Verify_ARevokedAward_IsRevoked_ThroughTheStatusList()
    {
        // Arrange
        await using var h = await CredentialTestHarness.CreateAsync();
        h.Definitions.Add("forklift");
        var award = await ManualAsync(h, "ana");
        await h.RunAsync(services => services.GetRequiredService<ICredentialAwardService>().RevokeAsync(award.ItemId, "admin", "Cheating"));

        // Act
        var result = await VerifyAsync(h, award.ItemId);

        // Assert
        Assert.Equal(CredentialVerificationOutcome.Revoked, result.Outcome);
        Assert.True(result.Revoked);
        Assert.True(result.SignatureValid);
    }

    [Fact]
    public async Task Verify_AnExpiredAward_IsExpired()
    {
        // Arrange
        await using var h = await CredentialTestHarness.CreateAsync();
        h.Definitions.Add("forklift").Achievement.ValidityMonths = 1;
        var award = await ManualAsync(h, "ana");

        // Act
        h.Clock.UtcNow = LearningTestHarness.Now.AddMonths(2);
        var result = await VerifyAsync(h, award.ItemId);

        // Assert
        Assert.Equal(CredentialVerificationOutcome.Expired, result.Outcome);
        Assert.True(result.Expired);
    }

    [Fact]
    public async Task Verify_AChangedToken_OrAKeyThatIsNotPublished_IsInvalid()
    {
        // Arrange
        await using var h = await CredentialTestHarness.CreateAsync();
        h.Definitions.Add("forklift");
        var forged = await ManualAsync(h, "ana");
        var unpublished = await ManualAsync(h, "ben");

        await h.RunAsync(async services =>
        {
            var store = services.GetRequiredService<CredentialAwardStore>();
            var stored = await store.FindByIdAsync(forged.ItemId);
            var parts = stored.Jwt.Split('.');
            var payload = JsonNode.Parse(Base64Url.DecodeFromChars(parts[1])).AsObject();

            payload["name"] = "Forged";
            stored.Jwt = parts[0] + "." + Base64Url.EncodeToString(Encoding.UTF8.GetBytes(payload.ToJsonString())) + "." + parts[2];
            await store.UpdateAsync(stored);

            var other = await store.FindByIdAsync(unpublished.ItemId);
            var key = new TestCredentialKeyRing().Current;

            other.Jwt = CredentialJws.Sign(CredentialJws.Read(other.Jwt).Payload, key.PrivateJwk, CredentialUrls.Key(CredentialTestHarness.BaseUrl, "unknown-key"));
            await store.UpdateAsync(other);
        });

        // Act
        var tampered = await VerifyAsync(h, forged.ItemId);
        var unknownKey = await VerifyAsync(h, unpublished.ItemId);

        // Assert
        Assert.Equal(CredentialVerificationOutcome.Invalid, tampered.Outcome);
        Assert.False(tampered.SignatureValid);
        Assert.Equal(CredentialVerificationOutcome.Invalid, unknownKey.Outcome);
    }

    [Fact]
    public async Task Verify_NoAward_IsNotFound()
    {
        // Arrange
        await using var h = await CredentialTestHarness.CreateAsync();

        // Act
        var result = await h.RunAsync(services => services.GetRequiredService<CredentialVerifier>().VerifyAsync(null));

        // Assert
        Assert.Equal(CredentialVerificationOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task TheStatusListCredential_IsSignedWithTheKeySet()
    {
        // Arrange
        await using var h = await CredentialTestHarness.CreateAsync();
        h.Definitions.Add("forklift");
        var award = await ManualAsync(h, "ana");

        // Act
        var jwt = await h.RunAsync(services => services.GetRequiredService<CredentialStatusLists>().SignAsync(award.StatusListNumber, CredentialTestHarness.BaseUrl));
        var (header, payload) = CredentialJws.Read(jwt);

        // Assert
        Assert.True(CredentialJws.Verify(jwt, h.Ring.Current.PublicJwk));
        Assert.Equal(CredentialUrls.Key(CredentialTestHarness.BaseUrl, h.Ring.Current.Kid), header["kid"].GetValue<string>());
        Assert.Equal(CredentialUrls.StatusList(CredentialTestHarness.BaseUrl, award.StatusListNumber), payload["jti"].GetValue<string>());
        Assert.Equal(CredentialUrls.Issuer(CredentialTestHarness.BaseUrl), payload["iss"].GetValue<string>());
        Assert.False(BitstringStatusList.IsSet(payload["credentialSubject"]["encodedList"].GetValue<string>(), award.StatusListIndex));
    }

    private static async Task<CredentialAward> ManualAsync(CredentialTestHarness h, string userId)
        => (await h.AwardAsync(new CredentialAwardRequest { DefinitionId = "forklift", UserId = userId, RuleKind = CredentialsConstants.RuleKinds.Manual, Actor = "admin" })).Award;

    private static Task<CredentialVerificationResult> VerifyAsync(CredentialTestHarness h, string awardId)
        => h.RunAsync(async services =>
        {
            var award = await services.GetRequiredService<ICredentialAwardService>().FindByIdAsync(awardId);

            return await services.GetRequiredService<CredentialVerifier>().VerifyAsync(award);
        });
}
