using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;

namespace CrestApps.OrchardCore.AI.Core;

public static class ModelStoreExtensions
{
    public static async ValueTask<IEnumerable<AIProfile>> GetAsync(this IModelStore<AIProfile> store, AIProfileType type)
    {
        return (await store.GetAllAsync()).Where(x => x.Type == type);
    }
}
