using System.Text.Json.Nodes;
using CrestApps.OrchardCore.OpenAI.Models;

namespace CrestApps.OrchardCore.OpenAI;

public interface IAIChatProfileManager
{
    /// <summary>
    /// Asynchronously deletes the specified AI chat profile.
    /// </summary>
    /// <param name="profile">The profile to be deleted.</param>
    /// <returns>A task that represents the asynchronous operation. The result is true if the deletion was successful, false otherwise.</returns>
    Task<bool> DeleteAsync(AIChatProfile profile);

    /// <summary>
    /// Asynchronously retrieves an AI chat profile by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the AI chat profile.</param>
    /// <returns>A task that represents the asynchronous operation. The result is the AIChatProfile corresponding to the specified ID, or null if not found.</returns>
    Task<AIChatProfile> FindByIdAsync(string id);

    /// <summary>
    /// Asynchronously retrieves an AI chat profile by its name.
    /// </summary>
    /// <param name="name">The name of the AI chat profile.</param>
    /// <returns>A task that represents the asynchronous operation. The result is the AIChatProfile corresponding to the specified ID, or null if not found.</returns>
    Task<AIChatProfile> FindByNameAsync(string name);

    /// <summary>
    /// Asynchronously creates a new AI chat profile with the given source and optional additional data.
    /// </summary>
    /// <param name="source">The source from which the profile is created.</param>
    /// <param name="data">Optional additional data associated with the profile. Defaults to null.</param>
    /// <returns>A task that represents the asynchronous operation. The result is the newly created AIChatProfile.</returns>
    Task<AIChatProfile> NewAsync(string source, JsonNode data = null);

    /// <summary>
    /// Asynchronously retrieves a paginated list of queries associated with AI chat profiles.
    /// </summary>
    /// <param name="page">The page number of the results to retrieve.</param>
    /// <param name="pageSize">The number of results per page.</param>
    /// <param name="context">The context for the query operation, which may include filtering, sorting, etc.</param>
    /// <returns>A task that represents the asynchronous operation. The result is an AIProfileResult containing the paginated query results.</returns>
    Task<AIProfileResult> PageQueriesAsync(int page, int pageSize, QueryContext context);

    /// <summary>
    /// Asynchronously saves (or updates) the specified AI chat profile.
    /// </summary>
    /// <param name="profile">The profile to be saved or updated.</param>
    /// <returns>A task that represents the asynchronous operation. No result is returned.</returns>
    Task SaveAsync(AIChatProfile profile);

    Task UpdateAsync(AIChatProfile profile, JsonNode data = null);

    Task<AIChatProfileValidateResult> ValidateAsync(AIChatProfile profile);
}
