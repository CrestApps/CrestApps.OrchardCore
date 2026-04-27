using CrestApps.Core.AI.Models;
using Microsoft.Extensions.Options;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.DataSources.Services;

internal sealed class AIDataSourceOptionsConfiguration : IConfigureOptions<AIDataSourceOptions>
{
    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIDataSourceOptionsConfiguration"/> class.
    /// </summary>
    /// <param name="siteService">The site service.</param>
    public AIDataSourceOptionsConfiguration(ISiteService siteService)
    {
        _siteService = siteService;
    }

    /// <summary>
    /// Configures the .
    /// </summary>
    /// <param name="options">The options.</param>
    public void Configure(AIDataSourceOptions options)
    {
        var settings = _siteService.GetSettings<AIDataSourceSettings>();

        options.DefaultStrictness = settings.DefaultStrictness;
        options.DefaultTopNDocuments = settings.DefaultTopNDocuments;
    }
}
