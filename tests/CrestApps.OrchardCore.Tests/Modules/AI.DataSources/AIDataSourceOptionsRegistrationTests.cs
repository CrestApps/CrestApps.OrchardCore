using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Services;
using CrestApps.OrchardCore.AI.DataSources.BackgroundTasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.AzureAI;
using OrchardCore.BackgroundTasks;
using OrchardCore.Indexing.Core;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Modules.AI.DataSources;

public sealed class AIDataSourceOptionsRegistrationTests
{
    [Fact]
    public void ConfigureServices_AppliesSiteOverridesWithoutLosingFieldMappings()
    {
        var settings = new AIDataSourceSettings
        {
            DefaultStrictness = 4,
            DefaultTopNDocuments = 9,
        };

        var services = new ServiceCollection();
        services.AddSingleton(CreateSiteService(settings));

        new CrestApps.OrchardCore.AI.DataSources.Startup().ConfigureServices(services);
        new CrestApps.OrchardCore.AI.DataSources.AzureAI.Startup(Mock.Of<IStringLocalizer<CrestApps.OrchardCore.AI.DataSources.AzureAI.Startup>>())
            .ConfigureServices(services);

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<AIDataSourceOptions>>().Value;
        var mapping = options.GetFieldMapping(AzureAISearchConstants.ProviderName, IndexingConstants.ContentsIndexSource);

        Assert.Equal(4, options.DefaultStrictness);
        Assert.Equal(9, options.DefaultTopNDocuments);
        Assert.NotNull(mapping);
        Assert.Equal("ContentItemId", mapping.DefaultKeyField);
    }

    [Fact]
    public void ConfigureServices_OverridesSharedQueueWithOrchardDeferredQueueProcessor()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(CreateSiteService(new AIDataSourceSettings()));

        new CrestApps.OrchardCore.AI.DataSources.Startup().ConfigureServices(services);

        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IAIDataSourceIndexingQueue) &&
                descriptor.ImplementationType?.Name == "OrchardAIDataSourceIndexingQueue" &&
                descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IBackgroundTask) &&
                descriptor.ImplementationType == typeof(DataSourceAlignmentBackgroundTask));

        using var serviceProvider = services.BuildServiceProvider();

        Assert.Equal(
            "OrchardAIDataSourceIndexingQueue",
            serviceProvider.GetRequiredService<IAIDataSourceIndexingQueue>().GetType().Name);
    }

    [Fact]
    public void ConfigureServices_RegistersOrchardIndexingAdapterAndProfileHandlers()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(CreateSiteService(new AIDataSourceSettings()));

        new CrestApps.OrchardCore.AI.DataSources.Startup().ConfigureServices(services);

        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IAIDataSourceIndexingService) &&
                descriptor.ImplementationType?.Name == "OrchardAIDataSourceIndexingServiceAdapter" &&
                descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(
            services,
            descriptor => descriptor.ImplementationType?.Name == "DataSourceIndexProfileHandler");
        Assert.Contains(
            services,
            descriptor => descriptor.ImplementationType?.Name == "DataSourceSourceIndexProfileHandler");
    }

    private static ISiteService CreateSiteService(AIDataSourceSettings settings)
    {
        var site = new Mock<ISite>();
        site.Setup(x => x.GetOrCreate<AIDataSourceSettings>())
            .Returns(settings);

        var siteService = new Mock<ISiteService>();
        siteService.Setup(x => x.GetSiteSettingsAsync())
            .ReturnsAsync(site.Object);

        return siteService.Object;
    }
}
