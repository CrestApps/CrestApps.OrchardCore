using CrestApps.AI.Models;
using CrestApps.AI.Services;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Documents.Services;

/// <summary>
/// Orchard Core site-settings implementation of <see cref="IInteractionDocumentSettingsProvider"/>.
/// </summary>
public sealed class OrchardCoreInteractionDocumentSettingsProvider : IInteractionDocumentSettingsProvider
{
    private readonly ISiteService _siteService;

    public OrchardCoreInteractionDocumentSettingsProvider(ISiteService siteService)
    {
        _siteService = siteService;
    }

    public Task<InteractionDocumentSettings> GetAsync() => _siteService.GetSettingsAsync<InteractionDocumentSettings>();
}
