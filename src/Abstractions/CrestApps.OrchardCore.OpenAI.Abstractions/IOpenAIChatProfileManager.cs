using System.Text.Json.Nodes;
using CrestApps.OrchardCore.OpenAI.Models;

namespace CrestApps.OrchardCore.OpenAI;

public interface IOpenAIChatProfileManager
{
    /// <summary>
    /// Asynchronously deletes the specified AI chat profile.
    /// </summary>
    /// <param name="profile">The profile to be deleted.</param>
    /// <returns>A task that represents the asynchronous operation. The result is true if the deletion was successful, false otherwise.</returns>
    Task<bool> DeleteAsync(OpenAIChatProfile profile);

    /// <summary>
    /// Asynchronously retrieves an AI chat profile by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the AI chat profile.</param>
    /// <returns>A task that represents the asynchronous operation. The result is the AIChatProfile corresponding to the specified ID, or null if not found.</returns>
    Task<OpenAIChatProfile> FindByIdAsync(string id);

    /// <summary>
    /// Asynchronously retrieves an AI chat profile by its name.
    /// </summary>
    /// <param name="name">The name of the AI chat profile.</param>
    /// <returns>A task that represents the asynchronous operation. The result is the AIChatProfile corresponding to the specified ID, or null if not found.</returns>
    Task<OpenAIChatProfile> FindByNameAsync(string name);

    /// <summary>
    /// Asynchronously creates a new AI chat profile with the given source and optional additional data.
    /// </summary>
    /// <param name="source">The source from which the profile is created.</param>
    /// <param name="data">Optional additional data associated with the profile. Defaults to null.</param>
    /// <returns>A task that represents the asynchronous operation. The result is the newly created AIChatProfile.</returns>
    Task<OpenAIChatProfile> NewAsync(string source, JsonNode data = null);

    /// <summary>
    /// Asynchronously retrieves a paginated list of profiles associated with AI chat profiles.
    /// </summary>
    /// <param name="page">The page number of the results to retrieve.</param>
    /// <param name="pageSize">The number of results per page.</param>
    /// <param name="context">The context for the query operation, which may include filtering, sorting, etc.</param>
    /// <returns>A task that represents the asynchronous operation. The result is an AIProfileResult containing the paginated query results.</returns>
    Task<OpenAIChatProfileResult> PageAsync(int page, int pageSize, OpenAIChatProfileQueryContext context);

    /// <summary>
    /// Asynchronously saves (or updates) the specified AI chat profile.
    /// </summary>
    /// <param name="profile">The profile to be saved or updated.</param>
    /// <returns>A task that represents the asynchronous operation. No result is returned.</returns>
    Task SaveAsync(OpenAIChatProfile profile);

    /// <summary>
    /// Asynchronously updates the specified AI chat profile with optional additional data.
    /// </summary>
    /// <param name="profile">The profile to be updated.</param>
    /// <param name="data">Optional additional data to update the profile with. Defaults to null.</param>
    /// <returns>A task that represents the asynchronous operation. No result is returned.</returns>
    Task UpdateAsync(OpenAIChatProfile profile, JsonNode data = null);

    /// <summary>
    /// Asynchronously validates the specified AI chat profile.
    /// </summary>
    /// <param name="profile">The profile to be validated.</param>
    /// <returns>A task that represents the asynchronous operation. The result is an OpenAIChatProfileValidateResult, which indicates whether the profile is valid or not.</returns>
    Task<OpenAIChatProfileValidateResult> ValidateAsync(OpenAIChatProfile profile);
}
