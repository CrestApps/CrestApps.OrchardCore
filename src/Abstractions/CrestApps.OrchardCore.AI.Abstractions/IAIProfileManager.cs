using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI;

public interface IAIProfileManager : IModelManager<AIProfile>
{
    /// <summary>
    /// Asynchronously retrieves an AI chat profile by its name.
    /// </summary>
    /// <param name="name">The name of the AI chat profile.</param>
    /// <returns>A ValueTask that represents the asynchronous operation. The result is the AIProfile corresponding to the specified ID, or null if not found.</returns>
    ValueTask<AIProfile> FindByNameAsync(string name);

    /// <summary>
    /// Asynchronously retrieves a paginated list of profiles associated with AI chat profiles.
    /// </summary>
    /// <param name="page">The page number of the results to retrieve.</param>
    /// <param name="pageSize">The number of results per page.</param>
    /// <param name="context">The context for the query operation, which may include filtering, sorting, etc.</param>
    /// <returns>A ValueTask that represents the asynchronous operation. The result is an AIProfileResult containing the paginated query results.</returns>
    ValueTask<PageResult<AIProfile>> PageAsync(int page, int pageSize, AIProfileQueryContext context);

    /// <summary>
    /// Asynchronously retrieves a collection of AI chat profiles of the specified type.
    /// </summary>
    /// <param name="type">The type of AI chat profiles to retrieve.</param>
    /// <returns>A ValueTask that represents the asynchronous operation. The result is an enumerable collection of AIProfile objects matching the specified type.</returns>
    ValueTask<IEnumerable<AIProfile>> GetAsync(AIProfileType type);
}
