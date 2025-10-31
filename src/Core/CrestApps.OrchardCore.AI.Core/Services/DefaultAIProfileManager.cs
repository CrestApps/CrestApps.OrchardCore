using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class DefaultAIProfileManager : NamedSourceCatalogManager<AIProfile>, IAIProfileManager
{
    public DefaultAIProfileManager(
        INamedSourceCatalog<AIProfile> profileStore,
        IEnumerable<ICatalogEntryHandler<AIProfile>> handlers,
        ILogger<DefaultAIProfileManager> logger)
        : base(profileStore, handlers, logger)
    {
    }

    public async ValueTask<IEnumerable<AIProfile>> GetAsync(AIProfileType type)
    {
        var profiles = await Catalog.GetAsync(type);

        foreach (var profile in profiles)
        {
            await LoadAsync(profile);
        }

        return profiles;
    }
}
