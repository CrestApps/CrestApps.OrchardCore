using CrestApps.Core.Telephony.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.DataProtection;
using CrestApps.Core.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class DefaultTelephonyUserTokenStoreTests
{
    [Fact]
    public async Task StoreAndGet_RoundTripsTokens_AndPersistsUser()
    {
        // Arrange
        var accessor = new FakeUserProfileStore();
        var store = new DefaultTelephonyUserTokenStore(accessor, new EphemeralDataProtectionProvider());

        var tokens = new TelephonyUserTokens
        {
            AccessToken = "access-token-value",
            RefreshToken = "refresh-token-value",
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1),
            TokenType = "Bearer",
            Scope = "calls",
        };

        // Act
        await store.StoreAsync("Dialpad", tokens, TestContext.Current.CancellationToken);
        var retrieved = await store.GetAsync("Dialpad", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("access-token-value", retrieved.AccessToken);
        Assert.Equal("refresh-token-value", retrieved.RefreshToken);
        Assert.Equal("Bearer", retrieved.TokenType);
        Assert.True(accessor.PersistCount > 0);
    }

    [Fact]
    public async Task StoreAsync_EncryptsTokensAtRest()
    {
        // Arrange
        var accessor = new FakeUserProfileStore();
        var store = new DefaultTelephonyUserTokenStore(accessor, new EphemeralDataProtectionProvider());

        var tokens = new TelephonyUserTokens
        {
            AccessToken = "super-secret-access",
            RefreshToken = "super-secret-refresh",
        };

        // Act
        await store.StoreAsync("Dialpad", tokens, TestContext.Current.CancellationToken);

        // Assert - what was persisted must not contain the plaintext tokens.
        var persisted = await accessor.FindAsync<TelephonyUserConnections>(TestContext.Current.CancellationToken);
        var serialized = System.Text.Json.JsonSerializer.Serialize(persisted);
        Assert.DoesNotContain("super-secret-access", serialized);
        Assert.DoesNotContain("super-secret-refresh", serialized);
    }

    [Fact]
    public async Task RemoveAsync_RemovesTokens()
    {
        // Arrange
        var accessor = new FakeUserProfileStore();
        var store = new DefaultTelephonyUserTokenStore(accessor, new EphemeralDataProtectionProvider());

        await store.StoreAsync("Dialpad", new TelephonyUserTokens { AccessToken = "a" }, TestContext.Current.CancellationToken);

        // Act
        await store.RemoveAsync("Dialpad", TestContext.Current.CancellationToken);
        var retrieved = await store.GetAsync("Dialpad", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(retrieved);
    }
}
