using System.Text;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Core.Security;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Microsoft.AspNetCore.DataProtection;

namespace CrestApps.OrchardCore.Tests.AgenticCommerce;

/// <summary>
/// The store's signing key is made the first time it is needed, published without its private part, and rotated
/// every 90 days; a retired key stays published for a week so signatures made just before the rotation still verify.
/// </summary>
public sealed class MerchantSigningKeyServiceTests
{
    private static readonly DateTime _now = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryDocumentManager<MerchantSigningKeysDocument> _documents = new();
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
        Assert.Single(_documents.Stored.Keys);
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
        Assert.Equal(1, _documents.Updates);
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
        Assert.DoesNotContain(_documents.Stored.Keys[0].PublicJwk, "\"d\"", StringComparison.Ordinal);
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
        Assert.Equal(2, _documents.Stored.Keys.Count);
        Assert.Single(_documents.Stored.Keys, entry => entry.RetiredUtc is null);
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
        Assert.Single(_documents.Stored.Keys);
    }

    private MerchantSigningKeyService CreateService()
        => new(_documents, _protection, _clock);

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
