using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;

namespace CrestApps.Core.AI.Profiles;
/// <summary>
/// Manages AI profiles with CRUD operations, name-based lookup,
/// and type-filtered retrieval. AI profiles define chat, agent, and embedding
/// configurations that drive AI completion behavior.
/// </summary>
public interface IAIProfileManager : INamedCatalogManager<AIProfile>
{
    /// <summary>
    /// Asynchronously retrieves a collection of AI chat profiles of the specified type.
    /// </summary>
    /// <param name="type">The type of AI chat profiles to retrieve.</param>
    /// <returns>A ValueTask that represents the asynchronous operation. The result is an enumerable collection of AIProfile objects matching the specified type.</returns>
    ValueTask<IEnumerable<AIProfile>> GetAsync(AIProfileType type);
}
