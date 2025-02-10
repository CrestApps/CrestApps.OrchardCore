using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI;

public static class AIProfileStoreExtensions
{
    public static async ValueTask<IEnumerable<AIProfile>> GetProfilesAsync(this IAIProfileStore store, AIProfileType type)
    {
        var profiles = await store.GetAllAsync();

        return profiles.Where(profile => profile.Type == type);
    }
}
