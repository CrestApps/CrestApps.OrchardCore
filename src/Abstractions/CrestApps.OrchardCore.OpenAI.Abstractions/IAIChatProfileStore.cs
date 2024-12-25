using CrestApps.OrchardCore.OpenAI.Models;

namespace CrestApps.OrchardCore.OpenAI;

public interface IAIChatProfileStore
{
    /// <summary>
    /// Deletes a specified AI chat profile from the store.
    /// </summary>
    /// <param name="profile">The AI chat profile to delete.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result is a boolean indicating
    /// whether the profile was successfully deleted.
    /// </returns>
    Task<bool> DeleteAsync(AIChatProfile profile);

    /// <summary>
    /// Finds an AI chat profile by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the AI chat profile.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result is the AI chat profile
    /// if found, or null if no profile with the specified id exists.
    /// </returns>
    Task<AIChatProfile> FindByIdAsync(string id);

    /// <summary>
    /// Finds an AI chat profile by its name.
    /// </summary>
    /// <param name="name">The name of the AI chat profile.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result is the AI chat profile
    /// if found, or null if no profile with the specified name exists.
    /// </returns>
    Task<AIChatProfile> FindByNameAsync(string name);

    Task<IEnumerable<AIChatProfile>> GetAllAsync();

    /// <summary>
    /// Pages through AI chat profiles based on the given query context and pagination parameters.
    /// </summary>
    /// <param name="page">The page number to retrieve (1-based index).</param>
    /// <param name="pageSize">The number of profiles per page.</param>
    /// <param name="context">The query context providing additional filtering and sorting options.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result is an AIProfileResult object
    /// containing a list of AI chat profiles and any relevant metadata for pagination.
    /// </returns>
    Task<AIProfileResult> PageAsync(int page, int pageSize, QueryContext context);

    /// <summary>
    /// Saves or updates an AI chat profile in the store.
    /// </summary>
    /// <param name="profile">The AI chat profile to save or update.</param>
    /// <returns>
    /// A task representing the asynchronous operation. This method does not return any value.
    /// </returns>
    Task SaveAsync(AIChatProfile profile);
}
