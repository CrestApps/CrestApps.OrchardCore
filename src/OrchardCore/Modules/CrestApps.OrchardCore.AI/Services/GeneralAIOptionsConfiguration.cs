using CrestApps.AI.Models;
using Microsoft.Extensions.Options;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Services;

internal sealed class GeneralAIOptionsConfiguration : IConfigureOptions<GeneralAIOptions>
{
    private readonly ISiteService _siteService;

    public GeneralAIOptionsConfiguration(ISiteService siteService)
    {
        _siteService = siteService;
    }

    public void Configure(GeneralAIOptions options)
    {
        var settings = _siteService.GetSettings<GeneralAISettings>();
        var overrides = GeneralAIOptions.FromSettings(settings);

        options.EnablePreemptiveMemoryRetrieval = overrides.EnablePreemptiveMemoryRetrieval;
        options.MaximumIterationsPerRequest = overrides.MaximumIterationsPerRequest;
        options.EnableDistributedCaching = overrides.EnableDistributedCaching;
        options.EnableOpenTelemetry = overrides.EnableOpenTelemetry;
    }
}
