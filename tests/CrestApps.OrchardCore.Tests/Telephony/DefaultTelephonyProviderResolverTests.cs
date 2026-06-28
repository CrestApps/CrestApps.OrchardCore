using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class DefaultTelephonyProviderResolverTests
{
    [Fact]
    public async Task GetAsync_WithoutName_ReturnsConfiguredDefaultProvider()
    {
        // Arrange
        var resolver = CreateResolver(
            BuildOptions(("A", typeof(FakeTelephonyProviderA), true), ("B", typeof(FakeTelephonyProviderB), true)),
            new TelephonySettings { DefaultProviderName = "B" });

        // Act
        var provider = await resolver.GetAsync();

        // Assert
        Assert.IsType<FakeTelephonyProviderB>(provider);
    }

    [Fact]
    public async Task GetAsync_WithName_ReturnsNamedProvider()
    {
        // Arrange
        var resolver = CreateResolver(
            BuildOptions(("A", typeof(FakeTelephonyProviderA), true), ("B", typeof(FakeTelephonyProviderB), true)),
            new TelephonySettings { DefaultProviderName = "B" });

        // Act
        var provider = await resolver.GetAsync("A");

        // Assert
        Assert.IsType<FakeTelephonyProviderA>(provider);
    }

    [Fact]
    public async Task GetAsync_WithUnknownName_ReturnsNull()
    {
        // Arrange
        var resolver = CreateResolver(
            BuildOptions(("A", typeof(FakeTelephonyProviderA), true)),
            new TelephonySettings());

        // Act
        var provider = await resolver.GetAsync("missing");

        // Assert
        Assert.Null(provider);
    }

    [Fact]
    public async Task GetAsync_WhenProviderDisabled_ReturnsNull()
    {
        // Arrange
        var resolver = CreateResolver(
            BuildOptions(("A", typeof(FakeTelephonyProviderA), false)),
            new TelephonySettings { DefaultProviderName = "A" });

        // Act
        var provider = await resolver.GetAsync("A");

        // Assert
        Assert.Null(provider);
    }

    [Fact]
    public async Task GetAsync_WhenNoDefaultConfigured_ReturnsNull()
    {
        // Arrange
        var resolver = CreateResolver(
            BuildOptions(("A", typeof(FakeTelephonyProviderA), true)),
            new TelephonySettings());

        // Act
        var provider = await resolver.GetAsync();

        // Assert
        Assert.Null(provider);
    }

    private static TelephonyProviderOptions BuildOptions(params (string Name, Type Type, bool Enabled)[] providers)
    {
        var options = new TelephonyProviderOptions();

        foreach (var (name, type, enabled) in providers)
        {
            options.TryAddProvider(name, new TelephonyProviderTypeOptions(type) { IsEnabled = enabled });
        }

        return options;
    }

    private static DefaultTelephonyProviderResolver CreateResolver(TelephonyProviderOptions options, TelephonySettings settings)
    {
        var siteService = SiteServiceFactory.Create(settings);
        var serviceProvider = new ServiceCollection().BuildServiceProvider();

        return new DefaultTelephonyProviderResolver(
            siteService,
            Options.Create(options),
            serviceProvider,
            NullLogger<DefaultTelephonyProviderResolver>.Instance);
    }
}
