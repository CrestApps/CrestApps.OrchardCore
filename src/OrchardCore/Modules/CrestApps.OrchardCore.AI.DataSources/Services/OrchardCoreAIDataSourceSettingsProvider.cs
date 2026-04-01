using CrestApps.AI.Models;
using CrestApps.AI.Services;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.DataSources.Services;

/// <summary>
/// Orchard Core site-settings implementation of <see cref="IAIDataSourceSettingsProvider"/>.
/// </summary>
public sealed class OrchardCoreAIDataSourceSettingsProvider : IAIDataSourceSettingsProvider
{
    private readonly ISiteService _siteService;
    public OrchardCoreAIDataSourceSettingsProvider(ISiteService siteService)
    {
        _siteService = siteService;
    }

    public Task<AIDataSourceSettings> GetAsync() => _siteService.GetSettingsAsync<AIDataSourceSettings>();
}
