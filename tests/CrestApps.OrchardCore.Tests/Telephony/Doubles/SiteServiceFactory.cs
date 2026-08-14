using Moq;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Telephony.Doubles;

/// <summary>
/// Builds a mocked <see cref="ISiteService"/> whose settings accessor returns the supplied settings,
/// mirroring how OrchardCore exposes <see cref="ISite.GetOrCreate{T}"/>.
/// </summary>
internal static class SiteServiceFactory
{
    public static ISiteService Create<T>(T settings)
        where T : class, new()
    {
        var site = new Mock<ISite>();
        site.Setup(s => s.GetOrCreate<T>()).Returns(settings);

        var siteService = new Mock<ISiteService>();
        siteService.Setup(s => s.GetSiteSettingsAsync()).ReturnsAsync(site.Object);
        siteService.Setup(s => s.LoadSiteSettingsAsync()).ReturnsAsync(site.Object);

        return siteService.Object;
    }
}
