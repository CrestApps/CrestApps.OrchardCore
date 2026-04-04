using CrestApps.AI.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Documents;

public sealed class InteractionDocumentOptionsRegistrationTests
{
    [Fact]
    public void ConfigureServices_AppliesSiteDocumentOverrides()
    {
        var settings = new InteractionDocumentSettings
        {
            IndexProfileName = "docs-index",
            TopN = 7,
        };

        var services = new ServiceCollection();
        services.AddSingleton(CreateSiteService(settings));

        new CrestApps.OrchardCore.AI.Documents.Startup().ConfigureServices(services);

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<InteractionDocumentOptions>>().Value;

        Assert.Equal("docs-index", options.IndexProfileName);
        Assert.Equal(7, options.TopN);
    }

    private static ISiteService CreateSiteService(InteractionDocumentSettings settings)
    {
        var site = new Mock<ISite>();
        site.Setup(x => x.As<InteractionDocumentSettings>())
            .Returns(settings);

        var siteService = new Mock<ISiteService>();
        siteService.Setup(x => x.GetSiteSettingsAsync())
            .ReturnsAsync(site.Object);

        return siteService.Object;
    }
}
