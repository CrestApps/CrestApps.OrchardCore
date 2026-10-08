using CrestApps.Core.AI.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Chat;

public sealed class PromptSecurityOptionsRegistrationTests
{
    [Fact]
    public void ConfigureServices_AppliesPromptSecuritySiteSettings()
    {
        var settings = new PromptSecurityOptions
        {
            EnableInjectionDetection = false,
            EnableOutputFiltering = false,
            EnableSecurityPreamble = false,
            EnableInputDelimiters = false,
            EnableAuditLogging = false,
            MaxPromptLength = 4096,
            BlockingThreshold = PromptRiskLevel.Critical,
            MaxMessagesPerWindow = 7,
            RateLimitWindow = TimeSpan.FromSeconds(90),
            MaxAnonymousSessionsPerWindow = 2,
            AnonymousSessionRateLimitWindow = TimeSpan.FromMinutes(15),
            AnonymousMessageRateLimitTiers =
            [
                new() { Limit = 3, Window = TimeSpan.FromSeconds(45) },
            ],
            AnonymousSessionStartRateLimitTiers =
            [
                new() { Limit = 4, Window = TimeSpan.FromMinutes(20) },
            ],
        };

        var services = new ServiceCollection();
        services.AddSingleton(CreateSiteService(settings));

        new CrestApps.OrchardCore.AI.Chat.Startup().ConfigureServices(services);

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<PromptSecurityOptions>>().Value;

        Assert.False(options.EnableInjectionDetection);
        Assert.False(options.EnableOutputFiltering);
        Assert.False(options.EnableSecurityPreamble);
        Assert.False(options.EnableInputDelimiters);
        Assert.False(options.EnableAuditLogging);
        Assert.Equal(4096, options.MaxPromptLength);
        Assert.Equal(PromptRiskLevel.Critical, options.BlockingThreshold);
        Assert.Equal(7, options.MaxMessagesPerWindow);
        Assert.Equal(TimeSpan.FromSeconds(90), options.RateLimitWindow);
        Assert.Equal(2, options.MaxAnonymousSessionsPerWindow);
        Assert.Equal(TimeSpan.FromMinutes(15), options.AnonymousSessionRateLimitWindow);

        var messageTier = Assert.Single(options.AnonymousMessageRateLimitTiers);
        Assert.Equal(3, messageTier.Limit);
        Assert.Equal(TimeSpan.FromSeconds(45), messageTier.Window);

        var sessionTier = Assert.Single(options.AnonymousSessionStartRateLimitTiers);
        Assert.Equal(4, sessionTier.Limit);
        Assert.Equal(TimeSpan.FromMinutes(20), sessionTier.Window);
    }

    [Fact]
    public void ConfigureServices_WithoutStoredTiers_KeepsCoreTierDefaults()
    {
        var defaults = new PromptSecurityOptions();

        var services = new ServiceCollection();
        services.AddSingleton(CreateSiteService(new PromptSecurityOptions()));

        new CrestApps.OrchardCore.AI.Chat.Startup().ConfigureServices(services);

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<PromptSecurityOptions>>().Value;

        Assert.Equal(
            defaults.AnonymousMessageRateLimitTiers.Select(tier => (tier.Limit, tier.Window)),
            options.AnonymousMessageRateLimitTiers.Select(tier => (tier.Limit, tier.Window)));
        Assert.Equal(
            defaults.AnonymousSessionStartRateLimitTiers.Select(tier => (tier.Limit, tier.Window)),
            options.AnonymousSessionStartRateLimitTiers.Select(tier => (tier.Limit, tier.Window)));
        Assert.Equal(defaults.MaxAnonymousSessionsPerWindow, options.MaxAnonymousSessionsPerWindow);
    }

    private static ISiteService CreateSiteService(PromptSecurityOptions settings)
    {
        var site = new Mock<ISite>();
        site.Setup(x => x.GetOrCreate<PromptSecurityOptions>())
            .Returns(settings);

        var siteService = new Mock<ISiteService>();
        siteService.Setup(x => x.GetSiteSettingsAsync())
            .ReturnsAsync(site.Object);

        return siteService.Object;
    }
}
