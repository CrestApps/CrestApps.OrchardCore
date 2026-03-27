using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;

namespace CrestApps.OrchardCore.AI;

public interface IAIProfileManager : INamedSourceCatalogManager<AIProfile>
{
    /// <summary>
    /// Asynchronously retrieves a collection of AI chat profiles of the specified type.
    /// </summary>
    /// <param name="type">The type of AI chat profiles to retrieve.</param>
    /// <returns>A ValueTask that represents the asynchronous operation. The result is an enumerable collection of AIProfile objects matching the specified type.</returns>
    ValueTask<IEnumerable<AIProfile>> GetAsync(AIProfileType type);
}
