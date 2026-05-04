using CrestApps.Core.AI.Documents.Models;
using CrestApps.Core.AI.Models;
using Microsoft.Extensions.Options;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Documents.Services;

internal sealed class InteractionDocumentOptionsConfiguration : IConfigureOptions<InteractionDocumentOptions>
{
    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="InteractionDocumentOptionsConfiguration"/> class.
    /// </summary>
    /// <param name="siteService">The site service.</param>
    public InteractionDocumentOptionsConfiguration(ISiteService siteService)
    {
        _siteService = siteService;
    }

    /// <summary>
    /// Configures the .
    /// </summary>
    /// <param name="options">The options.</param>
    public void Configure(InteractionDocumentOptions options)
    {
        var settings = _siteService.GetSettings<InteractionDocumentSettings>();

        options.IndexProfileName = settings.IndexProfileName;
        options.TopN = settings.TopN;
    }
}
