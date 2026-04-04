using CrestApps.AI.Models;
using Microsoft.Extensions.Options;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.DataSources.Services;

internal sealed class AIDataSourceOptionsConfiguration : IConfigureOptions<AIDataSourceOptions>
{
    private readonly ISiteService _siteService;

    public AIDataSourceOptionsConfiguration(ISiteService siteService)
    {
        _siteService = siteService;
    }

    public void Configure(AIDataSourceOptions options)
    {
        var settings = _siteService.GetSettings<AIDataSourceSettings>();
        var overrides = AIDataSourceOptions.FromSettings(settings);

        options.DefaultStrictness = overrides.DefaultStrictness;
        options.DefaultTopNDocuments = overrides.DefaultTopNDocuments;
    }
}
