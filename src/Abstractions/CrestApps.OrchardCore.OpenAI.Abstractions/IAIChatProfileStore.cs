using CrestApps.OrchardCore.OpenAI.Models;

namespace CrestApps.OrchardCore.OpenAI;

public interface IAIChatProfileStore
{
    /// <summary>
    /// Asynchronously deletes a specified AI chat profile from the store.
    /// </summary>
    /// <param name="profile">The AI chat profile to delete. Must not be null.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result is a boolean indicating
    /// whether the profile was successfully deleted. Returns <c>true</c> if the deletion succeeded,
    /// and <c>false</c> if the profile could not be deleted (e.g., if it does not exist).
    /// </returns>
    Task<bool> DeleteAsync(AIChatProfile profile);

    /// <summary>
    /// Asynchronously finds an AI chat profile by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier (ID) of the AI chat profile. Must not be null or empty.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result is the AI chat profile if found,
    /// or <c>null</c> if no profile with the specified identifier exists in the store.
    /// </returns>
    Task<AIChatProfile> FindByIdAsync(string id);

    /// <summary>
    /// Asynchronously finds an AI chat profile by its name.
    /// </summary>
    /// <param name="name">The name of the AI chat profile. Must not be null or empty.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result is the AI chat profile if found,
    /// or <c>null</c> if no profile with the specified name exists in the store.
    /// </returns>
    Task<AIChatProfile> FindByNameAsync(string name);

    /// <summary>
    /// Asynchronously retrieves all AI chat profiles in the store.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation. The task result is an <see cref="IEnumerable{AIChatProfile}"/>
    /// containing all AI chat profiles available in the store.
    /// </returns>
    Task<IEnumerable<AIChatProfile>> GetAllAsync();

    /// <summary>
    /// Asynchronously retrieves a paged list of AI chat profiles based on the specified pagination and filtering parameters.
    /// </summary>
    /// <param name="page">The page number to retrieve, where the index is 1-based.</param>
    /// <param name="pageSize">The number of profiles to retrieve per page.</param>
    /// <param name="context">The query context containing additional filtering, sorting, and search criteria. Can be null.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result is an <see cref="AIChatProfileResult"/> object,
    /// which contains the list of AI chat profiles for the requested page, along with metadata for pagination (such as total count, etc.).
    /// </returns>
    Task<AIChatProfileResult> PageAsync(int page, int pageSize, QueryContext context);

    /// <summary>
    /// Asynchronously saves or updates the specified AI chat profile in the store.
    /// </summary>
    /// <param name="profile">The AI chat profile to save or update. Must not be null.</param>
    /// <returns>
    /// A task representing the asynchronous operation. This method does not return any value.
    /// </returns>
    Task SaveAsync(AIChatProfile profile);
}
