using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class DefaultAIProfileManager : NamedCatalogManager<AIProfile>, IAIProfileManager
{
    private readonly IAIProfileStore _profileStore;

    public DefaultAIProfileManager(
        IAIProfileStore profileStore,
        IEnumerable<ICatalogEntryHandler<AIProfile>> handlers,
        ILogger<DefaultAIProfileManager> logger)
        : base(profileStore, handlers, logger)
    {
        _profileStore = profileStore;
    }

    public async ValueTask<IEnumerable<AIProfile>> GetAsync(AIProfileType type)
    {
        var profiles = await _profileStore.GetByTypeAsync(type);

        foreach (var profile in profiles)
        {
            await LoadAsync(profile);
        }

        return profiles;
    }
}
