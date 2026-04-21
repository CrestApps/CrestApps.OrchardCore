using CrestApps.Core.AI.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Memory;

public sealed class MemoryOptionsRegistrationTests
{
    [Fact]
    public void ConfigureServices_AppliesAIMemorySiteOverrides()
    {
        var services = CreateServices(
            new AIMemorySettings
            {
                IndexProfileName = " memory-index ",
                TopN = 8,
            },
            new MemoryMetadata());

        new CrestApps.OrchardCore.AI.Memory.Startup().ConfigureServices(services);

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<AIMemoryOptions>>().Value;

        Assert.Equal("memory-index", options.IndexProfileName);
        Assert.Equal(8, options.TopN);
    }

    [Fact]
    public void ConfigureServices_AppliesChatInteractionMemoryOverrides()
    {
        var services = CreateServices(
            new AIMemorySettings(),
            new MemoryMetadata
            {
                EnableUserMemory = false,
            });

        new CrestApps.OrchardCore.AI.Memory.Startup().ConfigureServices(services);

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<ChatInteractionMemoryOptions>>().Value;

        Assert.False(options.EnableUserMemory);
    }

    private static ServiceCollection CreateServices(
        AIMemorySettings memorySettings,
        MemoryMetadata chatInteractionMemorySettings)
    {
        var services = new ServiceCollection();
        services.AddSingleton(CreateSiteService(memorySettings, chatInteractionMemorySettings));

        return services;
    }

    private static ISiteService CreateSiteService(
        AIMemorySettings memorySettings,
        MemoryMetadata chatInteractionMemorySettings)
    {
        var site = new Mock<ISite>();
        site.Setup(x => x.GetOrCreate<AIMemorySettings>())
            .Returns(memorySettings);
        site.Setup(x => x.GetOrCreate<MemoryMetadata>())
            .Returns(chatInteractionMemorySettings);

        var siteService = new Mock<ISiteService>();
        siteService.Setup(x => x.GetSiteSettingsAsync())
            .ReturnsAsync(site.Object);

        return siteService.Object;
    }
}
