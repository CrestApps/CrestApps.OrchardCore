using CrestApps.AI.Models;
using CrestApps.Services;

namespace CrestApps.AI;

public static class CatalogExtensions
{
    public static async ValueTask<IEnumerable<AIProfile>> GetAsync(this INamedCatalog<AIProfile> store, AIProfileType type)
    {
        return (await store.GetAllAsync()).Where(x => x.Type == type);
    }
}
