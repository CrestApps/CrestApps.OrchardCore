using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Core.Models;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Moq;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// Covers the provider-agnostic half of internal extension calling: number resolution and the capability-gated
/// service dispatch that resolves a dialed extension to a target user before invoking the provider.
/// </summary>
public sealed class ExtensionCallingTests
{
    [Theory]
    [InlineData("1001", "1001")]
    [InlineData("  1001  ", "1001")]
    [InlineData("EXT-A", "ext-a")]
    public void NormalizeNumber_TrimsAndLowercases(string input, string expected)
    {
        Assert.Equal(expected, TelephonyExtension.NormalizeNumber(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeNumber_BlankIsNull(string input)
    {
        Assert.Null(TelephonyExtension.NormalizeNumber(input));
    }

    [Fact]
    public async Task Resolver_ReturnsFound_ForEnabledExtension()
    {
        var store = new Mock<ITelephonyExtensionStore>();
        store
            .Setup(s => s.FindByNumberAsync("1001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelephonyExtension
            {
                Number = "1001",
                UserId = "user-1",
                UserName = "bob",
                DisplayName = "Bob Smith",
            });

        var resolver = new TelephonyExtensionResolver(store.Object);

        var resolution = await resolver.ResolveAsync("1001", TestContext.Current.CancellationToken);

        Assert.True(resolution.Found);
        Assert.Equal("user-1", resolution.UserId);
        Assert.Equal("Bob Smith", resolution.DisplayName);
    }

    [Fact]
    public async Task Resolver_FallsBackToUserName_WhenNoDisplayName()
    {
        var store = new Mock<ITelephonyExtensionStore>();
        store
            .Setup(s => s.FindByNumberAsync("1002", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelephonyExtension { Number = "1002", UserId = "user-2", UserName = "carol" });

        var resolver = new TelephonyExtensionResolver(store.Object);

        var resolution = await resolver.ResolveAsync("1002", TestContext.Current.CancellationToken);

        Assert.True(resolution.Found);
        Assert.Equal("carol", resolution.DisplayName);
    }

    [Fact]
    public async Task Resolver_ReturnsNotFound_WhenNoExtension()
    {
        var store = new Mock<ITelephonyExtensionStore>();
        store
            .Setup(s => s.FindByNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TelephonyExtension)null);

        var resolver = new TelephonyExtensionResolver(store.Object);

        var resolution = await resolver.ResolveAsync("9999", TestContext.Current.CancellationToken);

        Assert.False(resolution.Found);
    }

    [Fact]
    public async Task DialExtension_WhenResolved_DispatchesToProviderWithTarget()
    {
        var provider = new ExtensionDialRecordingProvider();
        var service = CreateService(provider, ResolverFor("1001", "user-1", "Bob"));

        var result = await service.DialExtensionAsync(
            new ExtensionDialRequest { Extension = "1001" },
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(provider.LastDialRequest);
        Assert.Equal("user-1", provider.LastDialRequest.TargetUserId);
        Assert.Equal("Bob", provider.LastDialRequest.TargetDisplayName);
    }

    [Fact]
    public async Task DialExtension_WhenNotFound_FailsClosed_AndProviderNotCalled()
    {
        var provider = new ExtensionDialRecordingProvider();
        var service = CreateService(provider, new StubTelephonyExtensionResolver());

        var result = await service.DialExtensionAsync(
            new ExtensionDialRequest { Extension = "9999" },
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(provider.LastDialRequest);
    }

    [Fact]
    public async Task DialExtension_WhenExtensionBlank_Fails()
    {
        var provider = new ExtensionDialRecordingProvider();
        var service = CreateService(provider, new StubTelephonyExtensionResolver());

        var result = await service.DialExtensionAsync(
            new ExtensionDialRequest { Extension = "  " },
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(provider.LastDialRequest);
    }

    [Fact]
    public async Task DialExtension_WhenProviderDoesNotAdvertiseCapability_FailsClosed()
    {
        var provider = new ExtensionDialRecordingProvider { Capabilities = TelephonyCapabilities.None };
        var service = CreateService(provider, ResolverFor("1001", "user-1", "Bob"));

        var result = await service.DialExtensionAsync(
            new ExtensionDialRequest { Extension = "1001" },
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(provider.LastDialRequest);
    }

    [Fact]
    public async Task AddExtensionToConference_WithoutActiveCall_Fails()
    {
        var provider = new ExtensionDialRecordingProvider();
        var service = CreateService(provider, ResolverFor("1001", "user-1", "Bob"));

        var result = await service.AddExtensionToConferenceAsync(
            new ExtensionConferenceRequest { Extension = "1001" },
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(provider.LastConferenceRequest);
    }

    [Fact]
    public async Task AddExtensionToConference_WhenResolvedAndCapable_Dispatches()
    {
        var provider = new ExtensionDialRecordingProvider();
        var service = CreateService(provider, ResolverFor("1001", "user-1", "Bob"));

        var result = await service.AddExtensionToConferenceAsync(
            new ExtensionConferenceRequest
            {
                Extension = "1001",
                ActiveCall = new CallReference { CallId = "active-1" },
            },
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(provider.LastConferenceRequest);
        Assert.Equal("user-1", provider.LastConferenceRequest.TargetUserId);
        Assert.Equal("active-1", provider.LastConferenceRequest.ActiveCall.CallId);
    }

    private static StubTelephonyExtensionResolver ResolverFor(string number, string userId, string displayName)
        => new(new Dictionary<string, ExtensionResolution>
        {
            [number] = new ExtensionResolution
            {
                Found = true,
                Number = number,
                UserId = userId,
                UserName = userId,
                DisplayName = displayName,
            },
        });

    private static DefaultTelephonyService CreateService(ITelephonyProvider provider, ITelephonyExtensionResolver resolver)
        => new(
            new StubTelephonyProviderResolver(provider),
            new DefaultOutboundCallScreeningService([]),
            resolver,
            new PassThroughStringLocalizer<DefaultTelephonyService>());
}
