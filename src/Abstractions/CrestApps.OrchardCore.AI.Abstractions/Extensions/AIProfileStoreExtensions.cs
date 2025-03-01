using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.Services;

public static class AIProfileStoreExtensions
{
    public static async ValueTask<IEnumerable<AIProfile>> GetProfilesAsync(this INamedModelStore<AIProfile> store, AIProfileType type)
    {
        var profiles = await store.GetAllAsync();

        return profiles.Where(profile => profile.Type == type);
    }
}
