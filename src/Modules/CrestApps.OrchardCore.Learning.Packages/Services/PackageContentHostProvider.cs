using CrestApps.OrchardCore.Learning.Packages.Core.Services;
using CrestApps.OrchardCore.Learning.Packages.Models;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Learning.Packages.Services;

/// <summary>
/// Reads the package content host of the tenant from its site settings.
/// </summary>
public sealed class PackageContentHostProvider
{
    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PackageContentHostProvider"/> class.
    /// </summary>
    /// <param name="siteService">The site settings.</param>
    public PackageContentHostProvider(ISiteService siteService)
    {
        _siteService = siteService;
    }

    /// <summary>
    /// Gets the origin of the package content host.
    /// </summary>
    /// <returns>The origin, or <see langword="null"/> when none is set (strict mode).</returns>
    public async Task<string> GetOriginAsync()
    {
        var settings = await _siteService.GetSettingsAsync<LearningPackageContentHostSettings>();

        return PackageContentHost.TryNormalizeOrigin(settings?.BaseUrl, out var origin) ? origin : null;
    }
}
