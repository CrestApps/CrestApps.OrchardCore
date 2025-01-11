using CrestApps.OrchardCore.OpenAI.Models;

namespace CrestApps.OrchardCore.OpenAI;

public static class OpenAIChatProfileStoreExtensions
{
    public static async ValueTask<IEnumerable<OpenAIChatProfile>> GetProfilesAsync(this IOpenAIChatProfileStore store, OpenAIChatProfileType type)
    {
        var profiles = await store.GetAllAsync();

        return profiles.Where(profile => profile.Type == type);
    }
}
