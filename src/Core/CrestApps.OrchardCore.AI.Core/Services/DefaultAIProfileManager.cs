using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Services;
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

    public async ValueTask<IEnumerable<AIProfile>> GetAsync(AIProfileType type, CancellationToken cancellationToken = default)
    {
        var profiles = await _profileStore.GetByTypeAsync(type);

        foreach (var profile in profiles)
        {
            await LoadAsync(profile, cancellationToken);
        }

        return profiles;
    }
}
