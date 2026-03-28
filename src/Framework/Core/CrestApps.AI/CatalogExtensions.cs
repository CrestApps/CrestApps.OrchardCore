using CrestApps.AI.Models;
using CrestApps.Services;

namespace CrestApps.AI;

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
