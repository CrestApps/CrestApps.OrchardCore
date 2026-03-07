using CrestApps.AI.Models;
using CrestApps.Services;

namespace CrestApps.AI;

public static class AIProfileStoreExtensions
{
    public static async ValueTask<IEnumerable<AIProfile>> GetProfilesAsync(this INamedCatalog<AIProfile> store, AIProfileType type)
    {
        if (store is IAIProfileStore profileStore)
        {
            return await profileStore.GetByTypeAsync(type);
        }

        var profiles = await store.GetAllAsync();

        return profiles.Where(profile => profile.Type == type);
    }
}
