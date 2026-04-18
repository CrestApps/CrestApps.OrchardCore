using CrestApps.Core.AI.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Modules.AI;

public sealed class GeneralAIOptionsRegistrationTests
{
    [Fact]
    public void ConfigureServices_AppliesSiteGeneralAIOverrides()
    {
        var settings = new GeneralAISettings
        {
            EnablePreemptiveMemoryRetrieval = false,
            OverrideMaximumIterationsPerRequest = true,
            MaximumIterationsPerRequest = 12,
            OverrideEnableDistributedCaching = true,
            EnableDistributedCaching = false,
            OverrideEnableOpenTelemetry = true,
            EnableOpenTelemetry = true,
        };

        var services = new ServiceCollection();
        services.AddSingleton(CreateSiteService(settings));

        new CrestApps.OrchardCore.AI.Startup().ConfigureServices(services);

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<GeneralAIOptions>>().Value;

        Assert.False(options.EnablePreemptiveMemoryRetrieval);
        Assert.True(options.OverrideMaximumIterationsPerRequest);
        Assert.Equal(12, options.MaximumIterationsPerRequest);
        Assert.True(options.OverrideEnableDistributedCaching);
        Assert.False(options.EnableDistributedCaching);
        Assert.True(options.OverrideEnableOpenTelemetry);
        Assert.True(options.EnableOpenTelemetry);
    }

    private static ISiteService CreateSiteService(GeneralAISettings settings)
    {
        var site = new Mock<ISite>();
        site.Setup(x => x.GetOrCreate<GeneralAISettings>())
            .Returns(settings);

        var siteService = new Mock<ISiteService>();
        siteService.Setup(x => x.GetSiteSettingsAsync())
            .ReturnsAsync(site.Object);

        return siteService.Object;
    }
}
