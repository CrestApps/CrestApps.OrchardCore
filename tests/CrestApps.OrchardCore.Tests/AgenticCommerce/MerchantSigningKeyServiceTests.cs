using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Core.Security;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.SigningKeys;
using CrestApps.OrchardCore.SigningKeys.Core.Models;
using CrestApps.OrchardCore.SigningKeys.Core.Services;
using CrestApps.OrchardCore.Tests.Learning;
using CrestApps.OrchardCore.Tests.SigningKeys;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrchardCore.Secrets;

namespace CrestApps.OrchardCore.Tests.AgenticCommerce;

/// <summary>
/// The store's signing key, on the shared signing keys: made the first time it is needed and kept as a secret of
/// Orchard Core Secrets, published without its private part, and rotated every 90 days; a retired key stays published for
/// a week so signatures made just before the rotation still verify. The keys the store made before the move are
/// imported with their ids and dates, so the published key set is the same before and after, and the old key keeps
/// signing until it is due.
/// </summary>
public sealed class MerchantSigningKeyServiceTests
{
    private static readonly DateTime _now = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryDocumentManager<SigningKeysDocument> _ring = new();
    private readonly InMemoryDocumentManager<MerchantSigningKeysDocument> _legacy = new();
    private readonly InMemorySecretManager _secrets = new();
    private readonly EphemeralDataProtectionProvider _protection = new();
    private readonly TestClock _clock = new(_now);

    [Fact]
    public async Task GetSigningKeyAsync_TheFirstTime_CreatesOneKeyThatCanSign()
    {
        // Arrange
        var service = CreateService();

        // Act
        var key = await service.GetSigningKeyAsync(TestContext.Current.CancellationToken);
        var published = await service.GetPublishedKeysAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(key.HasPrivateKey);
        Assert.Equal("ES256", key.Alg);
        Assert.StartsWith("merchant-", key.Kid, StringComparison.Ordinal);
        Assert.Single(_ring.Stored.KeySets[MerchantSigningKeyService.KeySet]);
        Assert.Equal(typeof(TextSecret), _secrets.TypeOf(_ring.Stored.KeySets[MerchantSigningKeyService.KeySet][0].SecretName));
        Assert.True(Verifies(Sign(key), Assert.Single(published)));
    }

    [Fact]
    public async Task GetSigningKeyAsync_Again_ReturnsTheSameKey()
    {
        // Arrange
        var first = await CreateService().GetSigningKeyAsync(TestContext.Current.CancellationToken);

        // Act
        var second = await CreateService().GetSigningKeyAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(first.Kid, second.Kid);
        Assert.Equal(first.D, second.D);
        Assert.Equal(1, _ring.Updates);
    }

    [Fact]
    public async Task GetPublishedKeysAsync_ReturnsTheKeyWithoutItsPrivatePart()
    {
        // Arrange
        var service = CreateService();

        // Act
        var published = await service.GetPublishedKeysAsync(TestContext.Current.CancellationToken);

        // Assert
        var key = Assert.Single(published);
        var signingKey = await service.GetSigningKeyAsync(TestContext.Current.CancellationToken);

        Assert.False(key.HasPrivateKey);
        Assert.Null(key.D);
        Assert.Equal(signingKey.Kid, key.Kid);
        Assert.Equal(signingKey.X, key.X);
        Assert.Equal(signingKey.Y, key.Y);
        Assert.DoesNotContain("\"d\"", _ring.Stored.KeySets[MerchantSigningKeyService.KeySet][0].PublicJwk, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RotateAsync_KeepsTheOldKeyPublished()
    {
        // Arrange
        var old = await CreateService().GetSigningKeyAsync(TestContext.Current.CancellationToken);
        var service = CreateService();

        // Act
        await service.RotateAsync(TestContext.Current.CancellationToken);

        var current = await service.GetSigningKeyAsync(TestContext.Current.CancellationToken);
        var published = await CreateService().GetPublishedKeysAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEqual(old.Kid, current.Kid);
        Assert.Equal(new[] { old.Kid, current.Kid }.Order(StringComparer.Ordinal), published.Select(key => key.Kid).Order(StringComparer.Ordinal));
        Assert.True(Verifies(Sign(old), published.Single(key => key.Kid == old.Kid)));
    }

    [Fact]
    public async Task GetPublishedKeysAsync_EightDaysAfterARotation_NoLongerPublishesTheRetiredKey()
    {
        // Arrange
        var old = await CreateService().GetSigningKeyAsync(TestContext.Current.CancellationToken);

        await CreateService().RotateAsync(TestContext.Current.CancellationToken);

        var weekLater = await CreateService().GetPublishedKeysAsync(TestContext.Current.CancellationToken);

        _clock.UtcNow = _now.AddDays(8);

        // Act
        var published = await CreateService().GetPublishedKeysAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, weekLater.Count);

        var key = Assert.Single(published);

        Assert.NotEqual(old.Kid, key.Kid);
    }

    [Fact]
    public async Task RotateIfDueAsync_BeforeNinetyDays_KeepsTheKey()
    {
        // Arrange
        var key = await CreateService().GetSigningKeyAsync(TestContext.Current.CancellationToken);

        _clock.UtcNow = _now.AddDays(89);

        var service = CreateService();

        // Act
        var rotated = await service.RotateIfDueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.False(rotated);
        Assert.Equal(key.Kid, (await service.GetSigningKeyAsync(TestContext.Current.CancellationToken)).Kid);
    }

    [Fact]
    public async Task RotateIfDueAsync_AtNinetyDays_Rotates()
    {
        // Arrange
        var key = await CreateService().GetSigningKeyAsync(TestContext.Current.CancellationToken);

        _clock.UtcNow = _now + MerchantSigningKeyService.RotationAge;

        var service = CreateService();

        // Act
        var rotated = await service.RotateIfDueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(rotated);
        Assert.NotEqual(key.Kid, (await service.GetSigningKeyAsync(TestContext.Current.CancellationToken)).Kid);
        Assert.Equal(2, _ring.Stored.KeySets[MerchantSigningKeyService.KeySet].Count);
        Assert.Single(_ring.Stored.KeySets[MerchantSigningKeyService.KeySet], entry => entry.RetiredUtc is null);
    }

    [Fact]
    public async Task RotateIfDueAsync_WithoutAKey_CreatesOne()
    {
        // Arrange
        var service = CreateService();

        // Act
        var rotated = await service.RotateIfDueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(rotated);
        Assert.Single(_ring.Stored.KeySets[MerchantSigningKeyService.KeySet]);
    }

    [Fact]
    public async Task GetPublishedKeysAsync_AfterTheMove_PublishesTheSameKeySetAsBefore()
    {
        // Arrange: what the store kept before the move, and what it published then.
        var (current, signedBefore) = await SeedLegacyKeysAsync();
        var before = PublishedBeforeTheMove();

        // Act
        var after = await CreateService().GetPublishedKeysAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(before, after.Select(key => key.ToJson().ToJsonString()).ToArray());
        Assert.True(Verifies(signedBefore, after.Single(key => key.Kid == current.Kid)));
    }

    [Fact]
    public async Task GetSigningKeyAsync_AfterTheMove_StillSignsWithTheOldCurrentKey_AndLeavesTheOldDocument()
    {
        // Arrange
        var (current, _) = await SeedLegacyKeysAsync();
        var legacyBefore = System.Text.Json.JsonSerializer.Serialize(_legacy.Stored);

        // Act
        var key = await CreateService().GetSigningKeyAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(current.Kid, key.Kid);
        Assert.Equal(current.D, key.D);
        Assert.Equal(legacyBefore, System.Text.Json.JsonSerializer.Serialize(_legacy.Stored));
        Assert.Equal(3, _ring.Stored.KeySets[MerchantSigningKeyService.KeySet].Count);
        Assert.All(_ring.Stored.KeySets[MerchantSigningKeyService.KeySet], record => Assert.Contains(record.SecretName, _secrets.Names));
    }

    [Fact]
    public async Task RotateIfDueAsync_FromTheTaskBeforeAnyRequest_ImportsTheOldKeysInsteadOfMakingOne()
    {
        // Arrange
        var (current, _) = await SeedLegacyKeysAsync();
        var before = PublishedBeforeTheMove();

        // Act
        var rotated = await CreateService().RotateIfDueAsync(TestContext.Current.CancellationToken);
        var after = await CreateService().GetPublishedKeysAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.False(rotated);
        Assert.Equal(before, after.Select(key => key.ToJson().ToJsonString()).ToArray());
        Assert.Equal(current.Kid, (await CreateService().GetSigningKeyAsync(TestContext.Current.CancellationToken)).Kid);
    }

    [Fact]
    public async Task RotateIfDueAsync_AfterTheMove_RotatesTheOldKeyWhenItWouldHave()
    {
        // Arrange: the old current key was made 10 days ago.
        var (current, _) = await SeedLegacyKeysAsync();

        _clock.UtcNow = _now.AddDays(80);

        // Act
        var rotated = await CreateService().RotateIfDueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(rotated);
        Assert.NotEqual(current.Kid, (await CreateService().GetSigningKeyAsync(TestContext.Current.CancellationToken)).Kid);
    }

    private MerchantSigningKeyService CreateService()
    {
        var options = new SigningKeyOptions();

        MerchantSigningKeyService.Configure(options.GetOrAdd(MerchantSigningKeyService.KeySet));

        var importSource = new MerchantSigningKeyImportSource(_legacy, _protection, NullLogger<MerchantSigningKeyImportSource>.Instance);
        var ring = new SigningKeyRing(_ring, _secrets, new RecordingLock(), Options.Create(options), [importSource], _clock, NullLogger<SigningKeyRing>.Instance);

        return new MerchantSigningKeyService(ring);
    }

    // Writes the old document as the store's earlier version did: the current key made 10 days ago, one retired 2 days
    // ago (still published) and one retired 10 days ago (no longer published), the private halves protected.
    private async Task<(EcJsonWebKey Current, string SignedBefore)> SeedLegacyKeysAsync()
    {
        var protector = _protection.CreateProtector(MerchantSigningKeyImportSource.ProtectionPurpose);
        var document = new MerchantSigningKeysDocument();
        EcJsonWebKey current = null;

        foreach (var (kid, created, retired) in new (string, DateTime, DateTime?)[]
        {
            ("merchant-20260701-aaaaaaaa", _now.AddDays(-120), _now.AddDays(-10)),
            ("merchant-20260930-bbbbbbbb", _now.AddDays(-32), _now.AddDays(-2)),
            ("merchant-20261022-cccccccc", _now.AddDays(-10), null),
        })
        {
            using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var key = EcJsonWebKey.FromECDsa(ecdsa, kid, includePrivateKey: true);

            key.Use = "sig";
            key.Alg = "ES256";

            document.Keys.Add(new MerchantSigningKey
            {
                Kid = kid,
                ProtectedPrivateJwk = protector.Protect(key.ToJson(includePrivateKey: true).ToJsonString()),
                PublicJwk = key.ToJson(includePrivateKey: false).ToJsonString(),
                CreatedUtc = created,
                RetiredUtc = retired,
            });

            current = key;
        }

        await _legacy.UpdateAsync(document);

        return (current, Sign(current));
    }

    // The keys the store published before the move: the current one and those retired within the last week, as stored.
    private string[] PublishedBeforeTheMove()
        => _legacy.Stored.Keys
            .Where(key => key.RetiredUtc is null || key.RetiredUtc > _now - TimeSpan.FromDays(7))
            .Select(key => EcJsonWebKey.FromJson(JsonNode.Parse(key.PublicJwk) as JsonObject).ToJson().ToJsonString())
            .ToArray();

    private static string Sign(EcJsonWebKey key)
    {
        using var ecdsa = key.CreateECDsa();

        return JwsSigner.SignCompact(new JsonObject { ["alg"] = "ES256", ["kid"] = key.Kid }, Encoding.UTF8.GetBytes("{\"id\":\"order-1\"}"), ecdsa);
    }

    private static bool Verifies(string jws, EcJsonWebKey publicKey)
    {
        using var ecdsa = publicKey.CreateECDsa();

        return JwsSigner.TryVerifyCompact(jws, _ => ecdsa, out _, out _);
    }
}
