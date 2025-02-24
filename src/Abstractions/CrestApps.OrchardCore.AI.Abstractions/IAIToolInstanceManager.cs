using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI;

public interface IAIToolInstanceManager
{
    /// <summary>
    /// Asynchronously deletes the specified AI chat profile.
    /// </summary>
    /// <param name="instance">The profile to be deleted.</param>
    /// <returns>A ValueTask that represents the asynchronous operation. The result is true if the deletion was successful, false otherwise.</returns>
    ValueTask<bool> DeleteAsync(AIToolInstance instance);

    /// <summary>
    /// Asynchronously retrieves an AI chat profile by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the AI chat profile.</param>
    /// <returns>A ValueTask that represents the asynchronous operation. The result is the AIProfile corresponding to the specified ID, or null if not found.</returns>
    ValueTask<AIToolInstance> FindByIdAsync(string id);

    /// <summary>
    /// Asynchronously creates a new AI chat profile with the given source and optional additional data.
    /// </summary>
    /// <param name="source">The source from which the profile is created.</param>
    /// <param name="data">Optional additional data associated with the profile. Defaults to null.</param>
    /// <returns>A ValueTask that represents the asynchronous operation. The result is the newly created AIProfile.</returns>
    ValueTask<AIToolInstance> NewAsync(string source, JsonNode data = null);

    /// <summary>
    /// Asynchronously retrieves a paginated list of profiles associated with AI chat profiles.
    /// </summary>
    /// <param name="page">The page number of the results to retrieve.</param>
    /// <param name="pageSize">The number of results per page.</param>
    /// <param name="context">The context for the query operation, which may include filtering, sorting, etc.</param>
    /// <returns>A ValueTask that represents the asynchronous operation. The result is an AIToolTemplateResult containing the paginated query results.</returns>
    ValueTask<PageResult<AIToolInstance>> PageAsync(int page, int pageSize, QueryContext context);

    /// <summary>
    /// Asynchronously saves (or updates) the specified AI chat profile.
    /// </summary>
    /// <param name="instance">The profile to be saved or updated.</param>
    /// <returns>A ValueTask that represents the asynchronous operation. No result is returned.</returns>
    ValueTask SaveAsync(AIToolInstance instance);

    /// <summary>
    /// Asynchronously updates the specified AI chat profile with optional additional data.
    /// </summary>
    /// <param name="instance">The profile to be updated.</param>
    /// <param name="data">Optional additional data to update the profile with. Defaults to null.</param>
    /// <returns>A ValueTask that represents the asynchronous operation. No result is returned.</returns>
    ValueTask UpdateAsync(AIToolInstance instance, JsonNode data = null);

    /// <summary>
    /// Asynchronously validates the specified AI chat profile.
    /// </summary>
    /// <param name="template">The profile to be validated.</param>
    /// <returns>A ValueTask that represents the asynchronous operation. The result is an AIValidateResult, which indicates whether the profile is valid or not.</returns>
    ValueTask<AIValidateResult> ValidateAsync(AIToolInstance template);
}
