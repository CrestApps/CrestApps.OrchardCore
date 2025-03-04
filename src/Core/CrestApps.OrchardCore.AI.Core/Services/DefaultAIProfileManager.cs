using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class DefaultAIProfileManager : NamedModelManager<AIProfile>, IAIProfileManager
{
    public DefaultAIProfileManager(
        INamedModelStore<AIProfile> profileStore,
        IEnumerable<IModelHandler<AIProfile>> handlers,
        ILogger<DefaultAIProfileManager> logger)
        : base(profileStore, handlers, logger)
    {
    }

    public async ValueTask<IEnumerable<AIProfile>> GetAsync(AIProfileType type)
    {
        var profiles = await Store.GetAsync(type);

        foreach (var profile in profiles)
        {
            await LoadAsync(profile);
        }

        return profiles;
    }
}
