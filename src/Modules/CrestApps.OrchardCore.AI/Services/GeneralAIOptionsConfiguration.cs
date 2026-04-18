using CrestApps.Core.AI.Models;
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

        options.EnableAIUsageTracking = settings.EnableAIUsageTracking;
        options.EnablePreemptiveMemoryRetrieval = settings.EnablePreemptiveMemoryRetrieval;
        options.OverrideMaximumIterationsPerRequest = settings.OverrideMaximumIterationsPerRequest;
        options.MaximumIterationsPerRequest = settings.MaximumIterationsPerRequest;
        options.OverrideEnableDistributedCaching = settings.OverrideEnableDistributedCaching;
        options.EnableDistributedCaching = settings.EnableDistributedCaching;
        options.OverrideEnableOpenTelemetry = settings.OverrideEnableOpenTelemetry;
        options.EnableOpenTelemetry = settings.EnableOpenTelemetry;
    }
}
