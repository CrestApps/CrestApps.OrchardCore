using CrestApps.Core.AI.Documents.Models;
using CrestApps.Core.AI.Models;
using Microsoft.Extensions.Options;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Documents.Services;

internal sealed class InteractionDocumentOptionsConfiguration : IConfigureOptions<InteractionDocumentOptions>
{
    private readonly ISiteService _siteService;

    public InteractionDocumentOptionsConfiguration(ISiteService siteService)
    {
        _siteService = siteService;
    }

    public void Configure(InteractionDocumentOptions options)
    {
        var settings = _siteService.GetSettings<InteractionDocumentSettings>();
        var overrides = InteractionDocumentOptions.FromSettings(settings);

        options.IndexProfileName = overrides.IndexProfileName;
        options.TopN = overrides.TopN;
    }
}
