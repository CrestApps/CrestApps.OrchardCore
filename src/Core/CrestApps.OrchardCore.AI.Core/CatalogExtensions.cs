using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;

namespace CrestApps.OrchardCore.AI.Core;

public static class CatalogExtensions
{
    public static async ValueTask<IEnumerable<AIProfile>> GetAsync(this INamedCatalog<AIProfile> store, AIProfileType type)
    {
        if (store is IAIProfileStore profileStore)
        {
            return await profileStore.GetByTypeAsync(type);
        }

        return (await store.GetAllAsync()).Where(x => x.Type == type);
    }
}
