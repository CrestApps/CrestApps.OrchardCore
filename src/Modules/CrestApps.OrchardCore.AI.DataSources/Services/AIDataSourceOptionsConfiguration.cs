using CrestApps.Core.AI.Models;
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

        options.DefaultStrictness = settings.DefaultStrictness;
        options.DefaultTopNDocuments = settings.DefaultTopNDocuments;
    }
}
