using CrestApps.Core.AI.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.AzureAI;
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
