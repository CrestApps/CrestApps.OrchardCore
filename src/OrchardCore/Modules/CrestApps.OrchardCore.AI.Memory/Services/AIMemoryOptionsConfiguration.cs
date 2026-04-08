using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Memory.Models;
using Microsoft.Extensions.Options;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Memory.Services;

internal sealed class AIMemoryOptionsConfiguration : IConfigureOptions<AIMemoryOptions>
{
    private readonly ISiteService _siteService;

    public AIMemoryOptionsConfiguration(ISiteService siteService)
    {
        _siteService = siteService;
    }

    public void Configure(AIMemoryOptions options)
    {
        var settings = _siteService.GetSettings<AIMemorySettings>();

        options.IndexProfileName = string.IsNullOrWhiteSpace(settings.IndexProfileName) ? null : settings.IndexProfileName.Trim();
        options.TopN = settings.TopN;
    }
}
