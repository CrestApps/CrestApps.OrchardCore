using CrestApps.Core.AI.Memory;
using Microsoft.Extensions.Options;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Memory.Services;

internal sealed class AIMemoryOptionsConfiguration : IConfigureOptions<AIMemoryOptions>
{
    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIMemoryOptionsConfiguration"/> class.
    /// </summary>
    /// <param name="siteService">The site service.</param>
    public AIMemoryOptionsConfiguration(ISiteService siteService)
    {
        _siteService = siteService;
    }

    /// <summary>
    /// Configures the .
    /// </summary>
    /// <param name="options">The options.</param>
    public void Configure(AIMemoryOptions options)
    {
        var settings = _siteService.GetSettings<AIMemorySettings>();

        options.IndexProfileName = string.IsNullOrWhiteSpace(settings.IndexProfileName) ? null : settings.IndexProfileName.Trim();
        options.TopN = settings.TopN;
    }
}
