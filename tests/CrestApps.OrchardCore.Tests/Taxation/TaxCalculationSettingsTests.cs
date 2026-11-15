using CrestApps.OrchardCore.Taxation.Core.Models;
using CrestApps.OrchardCore.Taxation.Core.Services;
using CrestApps.OrchardCore.Taxation.Migrations;
using CrestApps.OrchardCore.Taxation.Models;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Settings;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Taxation;

/// <summary>
/// One Taxation setting says how the site rounds tax, for the checkout and invoices alike: a new site rounds each tax and
/// rate on the summed amount, and a site that already had tax rules keeps rounding each line, so no checkout total
/// changes on upgrade.
/// </summary>
public sealed class TaxCalculationSettingsTests
{
    [Fact]
    public void ANewSite_RoundsEachTaxAndRate()
        => Assert.Equal(TaxRoundingLevel.Rate, new TaxCalculationSettings().RoundingLevel);

    [Fact]
    public async Task TheProvider_ReadsTheSitesLevel()
    {
        // Arrange
        var site = new SiteSettings();
        site.Put(new TaxCalculationSettings { RoundingLevel = TaxRoundingLevel.Jurisdiction });
        var provider = new SiteTaxRoundingLevelProvider(SiteService(site).Object);

        // Act
        var level = await provider.GetRoundingLevelAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TaxRoundingLevel.Jurisdiction, level);
    }

    [Fact]
    public async Task TheUpgrade_KeepsLineRounding_ForASiteThatHadTaxRules()
    {
        // Arrange
        var site = new SiteSettings();
        var siteService = SiteService(site);

        // Act
        await TaxCalculationSettingsMigrations.KeepLineRoundingAsync(Services(siteService.Object));

        // Assert
        Assert.Equal(TaxRoundingLevel.Line, site.As<TaxCalculationSettings>().RoundingLevel);
        siteService.Verify(service => service.UpdateSiteSettingsAsync(site), Times.Once);
    }

    [Fact]
    public async Task TheUpgrade_KeepsTheLevelTheHostConfigured()
    {
        // Arrange
        var site = new SiteSettings();
        var siteService = SiteService(site);
        var services = new ServiceCollection()
            .AddSingleton(siteService.Object)
            .Configure<TaxationOptions>(options => options.RoundingLevel = TaxRoundingLevel.Transaction)
            .BuildServiceProvider();

        // Act
        await TaxCalculationSettingsMigrations.KeepLineRoundingAsync(services);

        // Assert
        Assert.Equal(TaxRoundingLevel.Transaction, site.As<TaxCalculationSettings>().RoundingLevel);
    }

    [Fact]
    public async Task TheUpgrade_KeepsALevelSomeoneChose()
    {
        // Arrange
        var site = new SiteSettings();
        site.Put(new TaxCalculationSettings { RoundingLevel = TaxRoundingLevel.Rate });
        var siteService = SiteService(site);

        // Act
        await TaxCalculationSettingsMigrations.KeepLineRoundingAsync(Services(siteService.Object));

        // Assert
        Assert.Equal(TaxRoundingLevel.Rate, site.As<TaxCalculationSettings>().RoundingLevel);
        siteService.Verify(service => service.UpdateSiteSettingsAsync(It.IsAny<ISite>()), Times.Never);
    }

    private static Mock<ISiteService> SiteService(SiteSettings site)
    {
        var siteService = new Mock<ISiteService>();
        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(site);
        siteService.Setup(service => service.LoadSiteSettingsAsync()).ReturnsAsync(site);

        return siteService;
    }

    private static ServiceProvider Services(ISiteService siteService)
        => new ServiceCollection().AddSingleton(siteService).BuildServiceProvider();
}
