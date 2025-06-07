using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;

namespace CrestApps.OrchardCore.AI.Core;

public static class AIProfileStoreExtensions
{
    public static async ValueTask<IEnumerable<AIProfile>> GetProfilesAsync(this INamedCatalog<AIProfile> store, AIProfileType type)
    {
        var profiles = await store.GetAllAsync();

        return profiles.Where(profile => profile.Type == type);
    }
}
