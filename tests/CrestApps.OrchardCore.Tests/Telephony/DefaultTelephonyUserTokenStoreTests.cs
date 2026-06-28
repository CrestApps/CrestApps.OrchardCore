using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.DataProtection;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class DefaultTelephonyUserTokenStoreTests
{
    [Fact]
    public async Task StoreAndGet_RoundTripsTokens_AndPersistsUser()
    {
        // Arrange
        var user = new FakeUser();
        var accessor = new FakeTelephonyUserAccessor(user);
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
        await store.StoreAsync("DialPad", tokens, TestContext.Current.CancellationToken);
        var retrieved = await store.GetAsync("DialPad", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("access-token-value", retrieved.AccessToken);
        Assert.Equal("refresh-token-value", retrieved.RefreshToken);
        Assert.Equal("Bearer", retrieved.TokenType);
        Assert.True(accessor.UpdateCount > 0);
    }

    [Fact]
    public async Task StoreAsync_EncryptsTokensAtRest()
    {
        // Arrange
        var user = new FakeUser();
        var accessor = new FakeTelephonyUserAccessor(user);
        var store = new DefaultTelephonyUserTokenStore(accessor, new EphemeralDataProtectionProvider());

        var tokens = new TelephonyUserTokens
        {
            AccessToken = "super-secret-access",
            RefreshToken = "super-secret-refresh",
        };

        // Act
        await store.StoreAsync("DialPad", tokens, TestContext.Current.CancellationToken);

        // Assert - the raw persisted properties must not contain the plaintext tokens.
        var serialized = user.Properties.ToJsonString();
        Assert.DoesNotContain("super-secret-access", serialized);
        Assert.DoesNotContain("super-secret-refresh", serialized);
    }

    [Fact]
    public async Task RemoveAsync_RemovesTokens()
    {
        // Arrange
        var user = new FakeUser();
        var accessor = new FakeTelephonyUserAccessor(user);
        var store = new DefaultTelephonyUserTokenStore(accessor, new EphemeralDataProtectionProvider());

        await store.StoreAsync("DialPad", new TelephonyUserTokens { AccessToken = "a" }, TestContext.Current.CancellationToken);

        // Act
        await store.RemoveAsync("DialPad", TestContext.Current.CancellationToken);
        var retrieved = await store.GetAsync("DialPad", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(retrieved);
    }
}
