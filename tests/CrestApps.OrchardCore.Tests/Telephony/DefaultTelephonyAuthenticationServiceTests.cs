using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class DefaultTelephonyAuthenticationServiceTests
{
    [Fact]
    public async Task GetStatusAsync_WithAccountLevelProvider_ReturnsConnectedWithoutAuthentication()
    {
        // Arrange
        var service = CreateService(
            new RecordingTelephonyProvider(),
            new TelephonySettings { DefaultProviderName = "Recording" },
            new FakeTelephonyUserTokenStore());

        // Act
        var status = await service.GetStatusAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(status.IsAvailable);
        Assert.False(status.RequiresAuthentication);
        Assert.True(status.IsConnected);
    }

    [Fact]
    public async Task GetStatusAsync_WithNoProvider_IsNotAvailable()
    {
        // Arrange
        var service = CreateService(
            provider: null,
            new TelephonySettings(),
            new FakeTelephonyUserTokenStore());

        // Act
        var status = await service.GetStatusAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.False(status.IsAvailable);
        Assert.False(status.IsConnected);
    }

    [Fact]
    public async Task GetStatusAsync_WithOAuthProviderAndNoTokens_RequiresAuthentication()
    {
        // Arrange
        var service = CreateService(
            new FakeAuthTelephonyProvider { RequiresUserAuthentication = true },
            new TelephonySettings { DefaultProviderName = "DialPad" },
            new FakeTelephonyUserTokenStore());

        // Act
        var status = await service.GetStatusAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(status.RequiresAuthentication);
        Assert.False(status.IsConnected);
        Assert.Equal(TelephonyAuthenticationSchemes.OAuth2, status.AuthenticationScheme);
    }

    [Fact]
    public async Task GetStatusAsync_WithOAuthProviderAndValidTokens_IsConnected()
    {
        // Arrange
        var tokenStore = new FakeTelephonyUserTokenStore();
        await tokenStore.StoreAsync("DialPad", new TelephonyUserTokens
        {
            AccessToken = "valid",
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1),
        }, TestContext.Current.CancellationToken);

        var service = CreateService(
            new FakeAuthTelephonyProvider { RequiresUserAuthentication = true },
            new TelephonySettings { DefaultProviderName = "DialPad" },
            tokenStore);

        // Act
        var status = await service.GetStatusAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(status.RequiresAuthentication);
        Assert.True(status.IsConnected);
    }

    [Fact]
    public async Task CompleteAuthorizationAsync_StoresTokens()
    {
        // Arrange
        var tokenStore = new FakeTelephonyUserTokenStore();
        var service = CreateService(
            new FakeAuthTelephonyProvider { RequiresUserAuthentication = true },
            new TelephonySettings { DefaultProviderName = "DialPad" },
            tokenStore);

        // Act
        var result = await service.CompleteAuthorizationAsync("code", "https://site.test/callback", TestContext.Current.CancellationToken);
        var stored = await tokenStore.GetAsync("DialPad", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result);
        Assert.NotNull(stored);
        Assert.Equal("exchanged", stored.AccessToken);
    }

    private static DefaultTelephonyAuthenticationService CreateService(
        ITelephonyProvider provider,
        TelephonySettings settings,
        ITelephonyUserTokenStore tokenStore)
    {
        var siteService = SiteServiceFactory.Create(settings);
        var resolver = new StubTelephonyProviderResolver(provider);

        return new DefaultTelephonyAuthenticationService(siteService, resolver, tokenStore, new StubClock());
    }
}
