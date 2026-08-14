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
