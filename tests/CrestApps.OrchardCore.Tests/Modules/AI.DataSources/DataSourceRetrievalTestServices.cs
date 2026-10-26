using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Modules.AI.DataSources;

/// <summary>
/// The services a data source editor's retrieval settings read. The site defaults differ from the built-in ones, so a
/// test can tell a value the site default filled in from the fallback the settings use when the site has none.
/// </summary>
internal static class DataSourceRetrievalTestServices
{
    public const int SiteDefaultStrictness = 4;

    public const int SiteDefaultTopNDocuments = 7;

    public static ISiteService SiteService()
    {
        var site = new Mock<ISite>();
        site.Setup(s => s.GetOrCreate<AIDataSourceSettings>()).Returns(new AIDataSourceSettings
        {
            DefaultStrictness = SiteDefaultStrictness,
            DefaultTopNDocuments = SiteDefaultTopNDocuments,
        });

        var siteService = new Mock<ISiteService>();
        siteService.Setup(s => s.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        return siteService.Object;
    }

    public static IODataValidator ODataValidator()
        => Mock.Of<IODataValidator>();

    public static IAIDataSourceStore DataSourceStore()
        => Mock.Of<IAIDataSourceStore>();

    public static IStringLocalizer<T> Localizer<T>()
        => new StringLocalizer<T>(new SourceTextLocalizerFactory());
}
