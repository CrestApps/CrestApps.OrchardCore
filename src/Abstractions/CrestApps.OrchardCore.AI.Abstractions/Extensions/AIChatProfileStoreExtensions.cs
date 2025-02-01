using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI;

public static class AIChatProfileStoreExtensions
{
    public static async ValueTask<IEnumerable<AIChatProfile>> GetProfilesAsync(this IAIChatProfileStore store, AIChatProfileType type)
    {
        var profiles = await store.GetAllAsync();

        return profiles.Where(profile => profile.Type == type);
    }
}
