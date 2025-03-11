using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Models;

namespace CrestApps.OrchardCore.Services;

public interface IModelManager<T>
{
    /// <summary>
    /// Asynchronously deletes the specified model.
    /// </summary>
    /// <param name="model">The model to be deleted.</param>
    /// <returns>A ValueTask that represents the asynchronous operation. The result is true if the deletion was successful, false otherwise.</returns>
    ValueTask<bool> DeleteAsync(T model);

    /// <summary>
    /// Asynchronously retrieves an model by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the model.</param>
    /// <returns>A ValueTask that represents the asynchronous operation. The result is the <typeparamref name="T"/> corresponding to the specified ID, or null if not found.</returns>
    ValueTask<T> FindByIdAsync(string id);

    /// <summary>
    /// Asynchronously creates a new model with the given source and optional additional data.
    /// </summary>
    /// <param name="data">Optional additional data associated with the profile. Defaults to null.</param>
    /// <returns>A ValueTask that represents the asynchronous operation. The result is the newly created model.</returns>
    ValueTask<T> NewAsync(JsonNode data = null);

    /// <summary>
    /// Asynchronously retrieves a list of all model deployments.
    /// </summary>
    /// <returns>
    /// A ValueTask that represents the asynchronous operation. The result is an <see cref="IEnumerable{T}"/>
    /// containing all model deployments.
    /// </returns>
    ValueTask<IEnumerable<T>> GetAllAsync();

    /// <summary>
    /// Asynchronously retrieves a paginated list of models associated with models.
    /// </summary>
    /// <param name="page">The page number of the results to retrieve.</param>
    /// <param name="pageSize">The number of results per page.</param>
    /// <param name="context">The context for the query operation, which may include filtering, sorting, etc.</param>
    /// <returns>A ValueTask that represents the asynchronous operation. The result is an AIToolTemplateResult containing the paginated query results.</returns>
    ValueTask<PageResult<T>> PageAsync<TQuery>(int page, int pageSize, TQuery context)
        where TQuery : QueryContext;

    /// <summary>
    /// Asynchronously create the given model.
    /// </summary>
    /// <param name="model">The model to be created.</param>
    /// <returns>A ValueTask that represents the asynchronous operation. No result is returned.</returns>
    ValueTask CreateAsync(T model);

    /// <summary>
    /// Asynchronously updates the specified model with optional additional data.
    /// </summary>
    /// <param name="model">The profile to be updated.</param>
    /// <param name="data">Optional additional data to update the profile with. Defaults to null.</param>
    /// <returns>A ValueTask that represents the asynchronous operation. No result is returned.</returns>
    ValueTask UpdateAsync(T model, JsonNode data = null);

    /// <summary>
    /// Asynchronously validates the specified model.
    /// </summary>
    /// <param name="model">The profile to be validated.</param>
    /// <returns>A ValueTask that represents the asynchronous operation. The result is an AIValidateResult, which indicates whether the profile is valid or not.</returns>
    ValueTask<ValidationResultDetails> ValidateAsync(T model);
}
