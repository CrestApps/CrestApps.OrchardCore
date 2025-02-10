using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI;

public interface IAIProfileManager
{
    /// <summary>
    /// Asynchronously deletes the specified AI chat profile.
    /// </summary>
    /// <param name="profile">The profile to be deleted.</param>
    /// <returns>A ValueTask that represents the asynchronous operation. The result is true if the deletion was successful, false otherwise.</returns>
    ValueTask<bool> DeleteAsync(AIProfile profile);

    /// <summary>
    /// Asynchronously retrieves an AI chat profile by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the AI chat profile.</param>
    /// <returns>A ValueTask that represents the asynchronous operation. The result is the AIProfile corresponding to the specified ID, or null if not found.</returns>
    ValueTask<AIProfile> FindByIdAsync(string id);

    /// <summary>
    /// Asynchronously retrieves an AI chat profile by its name.
    /// </summary>
    /// <param name="name">The name of the AI chat profile.</param>
    /// <returns>A ValueTask that represents the asynchronous operation. The result is the AIProfile corresponding to the specified ID, or null if not found.</returns>
    ValueTask<AIProfile> FindByNameAsync(string name);

    /// <summary>
    /// Asynchronously creates a new AI chat profile with the given source and optional additional data.
    /// </summary>
    /// <param name="source">The source from which the profile is created.</param>
    /// <param name="data">Optional additional data associated with the profile. Defaults to null.</param>
    /// <returns>A ValueTask that represents the asynchronous operation. The result is the newly created AIProfile.</returns>
    ValueTask<AIProfile> NewAsync(string source, JsonNode data = null);

    /// <summary>
    /// Asynchronously retrieves a paginated list of profiles associated with AI chat profiles.
    /// </summary>
    /// <param name="page">The page number of the results to retrieve.</param>
    /// <param name="pageSize">The number of results per page.</param>
    /// <param name="context">The context for the query operation, which may include filtering, sorting, etc.</param>
    /// <returns>A ValueTask that represents the asynchronous operation. The result is an AIProfileResult containing the paginated query results.</returns>
    ValueTask<AIProfileResult> PageAsync(int page, int pageSize, AIProfileQueryContext context);

    /// <summary>
    /// Asynchronously saves (or updates) the specified AI chat profile.
    /// </summary>
    /// <param name="profile">The profile to be saved or updated.</param>
    /// <returns>A ValueTask that represents the asynchronous operation. No result is returned.</returns>
    ValueTask SaveAsync(AIProfile profile);

    /// <summary>
    /// Asynchronously updates the specified AI chat profile with optional additional data.
    /// </summary>
    /// <param name="profile">The profile to be updated.</param>
    /// <param name="data">Optional additional data to update the profile with. Defaults to null.</param>
    /// <returns>A ValueTask that represents the asynchronous operation. No result is returned.</returns>
    ValueTask UpdateAsync(AIProfile profile, JsonNode data = null);

    /// <summary>
    /// Asynchronously validates the specified AI chat profile.
    /// </summary>
    /// <param name="profile">The profile to be validated.</param>
    /// <returns>A ValueTask that represents the asynchronous operation. The result is an AIChatProfileValidateResult, which indicates whether the profile is valid or not.</returns>
    ValueTask<AIProfileValidateResult> ValidateAsync(AIProfile profile);

    /// <summary>
    /// Asynchronously retrieves a collection of AI chat profiles of the specified type.
    /// </summary>
    /// <param name="type">The type of AI chat profiles to retrieve.</param>
    /// <returns>A ValueTask that represents the asynchronous operation. The result is an enumerable collection of AIProfile objects matching the specified type.</returns>
    ValueTask<IEnumerable<AIProfile>> GetAsync(AIProfileType type);
}
