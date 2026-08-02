using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Options;

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
        Assert.Equal(TelephonyConstants.AuthenticationSchemes.OAuth2, status.AuthenticationScheme);
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
        var result = await service.CompleteAuthorizationAsync("code", "https://site.test/callback", codeVerifier: null, TestContext.Current.CancellationToken);
        var stored = await tokenStore.GetAsync("DialPad", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.NotNull(stored);
        Assert.Equal("exchanged", stored.AccessToken);
    }

    [Fact]
    public async Task GetAuthorizationUrlAsync_WhenProviderSupportsPkce_GeneratesCodeVerifier()
    {
        // Arrange
        var service = CreateService(
            new FakeAuthTelephonyProvider { RequiresUserAuthentication = true, SupportsProofKeyForCodeExchange = true },
            new TelephonySettings { DefaultProviderName = "DialPad" },
            new FakeTelephonyUserTokenStore());

        // Act
        var request = await service.GetAuthorizationUrlAsync("https://site.test/callback", "state-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(request);
        Assert.False(string.IsNullOrEmpty(request.Url));
        Assert.False(string.IsNullOrEmpty(request.CodeVerifier));
    }

    [Fact]
    public async Task GetAuthorizationUrlAsync_WhenProviderDoesNotSupportPkce_DoesNotGenerateCodeVerifier()
    {
        // Arrange
        var service = CreateService(
            new FakeAuthTelephonyProvider { RequiresUserAuthentication = true, SupportsProofKeyForCodeExchange = false },
            new TelephonySettings { DefaultProviderName = "DialPad" },
            new FakeTelephonyUserTokenStore());

        // Act
        var request = await service.GetAuthorizationUrlAsync("https://site.test/callback", "state-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(request);
        Assert.Null(request.CodeVerifier);
    }

    [Fact]
    public async Task DisconnectAsync_RevokesTokensBeforeRemovingThem()
    {
        // Arrange
        var tokenStore = new FakeTelephonyUserTokenStore();
        await tokenStore.StoreAsync("DialPad", new TelephonyUserTokens
        {
            AccessToken = "valid",
        }, TestContext.Current.CancellationToken);

        var provider = new FakeAuthTelephonyProvider { RequiresUserAuthentication = true };
        var service = CreateService(
            provider,
            new TelephonySettings { DefaultProviderName = "DialPad" },
            tokenStore);

        // Act
        await service.DisconnectAsync(TestContext.Current.CancellationToken);
        var stored = await tokenStore.GetAsync("DialPad", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(provider.RevokedTokens);
        Assert.Equal("valid", provider.RevokedTokens.AccessToken);
        Assert.Null(stored);
    }

    [Fact]
    public async Task GetValidTokensAsync_WhenTokensExpireAndCallersRace_RefreshesOnlyOnce()
    {
        // Arrange
        var tokenStore = new FakeTelephonyUserTokenStore();
        await tokenStore.StoreAsync("DialPad", new TelephonyUserTokens
        {
            AccessToken = "expired",
            RefreshToken = "refresh",
            ExpiresUtc = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
        }, TestContext.Current.CancellationToken);

        var provider = new FakeAuthTelephonyProvider
        {
            RequiresUserAuthentication = true,
            RefreshResult = new TelephonyUserTokens
            {
                AccessToken = "refreshed",
                RefreshToken = "rotated",
                ExpiresUtc = new DateTimeOffset(2026, 1, 1, 1, 0, 0, TimeSpan.Zero),
            },
        };

        var distributedLock = new FakeDistributedLock();
        var service = CreateService(
            provider,
            new TelephonySettings { DefaultProviderName = "DialPad" },
            tokenStore,
            distributedLock);

        // Act
        // Park the first refresh inside the critical section, then wait until the second caller has provably
        // reached the lock and is contending for it before letting the first finish. This exercises the
        // serialization path rather than the two races coincidentally running in order.
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        provider.RefreshGate = gate.Task;

        var first = Task.Run(() => service.GetValidTokensAsync("DialPad", CancellationToken.None));
        await provider.RefreshStarted;

        var second = Task.Run(() => service.GetValidTokensAsync("DialPad", CancellationToken.None));
        await distributedLock.WaitForAttemptAsync(2);
        gate.SetResult();

        var results = await Task.WhenAll(first, second);

        // Assert
        Assert.Equal(1, provider.RefreshCount);
        Assert.All(results, tokens => Assert.Equal("refreshed", tokens.AccessToken));
    }

    private static DefaultTelephonyAuthenticationService CreateService(
        ITelephonyProvider provider,
        TelephonySettings settings,
        ITelephonyUserTokenStore tokenStore)
        => CreateService(provider, settings, tokenStore, new FakeDistributedLock());

    private static DefaultTelephonyAuthenticationService CreateService(
        ITelephonyProvider provider,
        TelephonySettings settings,
        ITelephonyUserTokenStore tokenStore,
        FakeDistributedLock distributedLock)
    {
        var siteService = SiteServiceFactory.Create(settings);
        var resolver = new StubTelephonyProviderResolver(provider);
        var userAccessor = new FakeTelephonyUserAccessor(new FakeUser { UserName = "tester" });
        var options = Options.Create(new TelephonyCoordinationOptions());

        return new DefaultTelephonyAuthenticationService(
            siteService,
            resolver,
            tokenStore,
            userAccessor,
            distributedLock,
            new StubClock(),
            options);
    }
}
