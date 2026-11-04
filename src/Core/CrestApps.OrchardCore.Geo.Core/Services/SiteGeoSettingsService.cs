using CrestApps.OrchardCore.Geo.Models;
using CrestApps.OrchardCore.Geo.Services;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Geo.Core.Services;

/// <summary>
/// Reads the Geo settings from the site settings.
/// </summary>
public sealed class SiteGeoSettingsService : IGeoSettingsService
{
    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SiteGeoSettingsService"/> class.
    /// </summary>
    /// <param name="siteService">The site service.</param>
    public SiteGeoSettingsService(ISiteService siteService)
    {
        _siteService = siteService;
    }

    /// <inheritdoc/>
    public Task<GeoSettings> GetAsync() => _siteService.GetSettingsAsync<GeoSettings>();
}
