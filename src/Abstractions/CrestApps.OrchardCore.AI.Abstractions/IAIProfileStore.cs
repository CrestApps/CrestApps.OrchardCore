using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI;

public interface IAIProfileStore : IModelStore<AIProfile>
{
    /// <summary>
    /// Asynchronously retrieves a paged list of AI chat profiles based on the specified pagination and filtering parameters.
    /// </summary>
    /// <param name="page">The page number to retrieve, where the index is 1-based.</param>
    /// <param name="pageSize">The number of profiles to retrieve per page.</param>
    /// <param name="context">The query context containing additional filtering, sorting, and search criteria. Can be null.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result is an <see cref="StoreResult"/> object,
    /// which contains the list of AI chat profiles for the requested page, along with metadata for pagination (such as total count, etc.).
    /// </returns>
    ValueTask<PageResult<AIProfile>> PageAsync(int page, int pageSize, AIProfileQueryContext context);

    /// <summary>
    /// Asynchronously finds a profile by its name.
    /// </summary>
    /// <param name="name">The name of the profile. Must not be null or empty.</param>
    /// <returns>
    /// A ValueTask representing the asynchronous operation. The task result is the <see cref="T"/> if found,
    /// or <c>null</c> if no profile with the specified name exists in the store.
    /// </returns>
    ValueTask<AIProfile> FindByNameAsync(string name);
}
