using CrestApps.Core.Hosting;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Core.Hosting;

/// <summary>
/// Reports the base URL an Orchard Core tenant is configured with.
/// </summary>
public sealed class SiteSettingsPublicBaseUrlAccessor : IPublicBaseUrlAccessor
{
    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SiteSettingsPublicBaseUrlAccessor"/> class.
    /// </summary>
    /// <param name="siteService">The site service.</param>
    public SiteSettingsPublicBaseUrlAccessor(ISiteService siteService)
    {
        _siteService = siteService;
    }

    /// <inheritdoc/>
    public async ValueTask<string> GetBaseUrlAsync(CancellationToken cancellationToken = default)
    {
        var site = await _siteService.GetSiteSettingsAsync();
        var baseUrl = site?.BaseUrl;

        return string.IsNullOrWhiteSpace(baseUrl) ? null : baseUrl.TrimEnd('/');
    }
}
