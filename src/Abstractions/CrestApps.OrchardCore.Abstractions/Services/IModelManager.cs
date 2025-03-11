using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Models;

namespace CrestApps.OrchardCore.Services;

public interface IModelManager<T>
{
    /// <summary>
    /// Asynchronously deletes the specified model.
    /// </summary>
    /// <param name="model">The model to be deleted.</param>
    /// <returns>
    /// A <see cref="ValueTask{bool}"/> that represents the asynchronous operation.
    /// The result is <c>true</c> if the deletion was successful, <c>false</c> otherwise.
    /// </returns>
    ValueTask<bool> DeleteAsync(T model);

    /// <summary>
    /// Asynchronously retrieves a model by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the model.</param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation.
    /// The result is the model corresponding to the specified ID, or <c>null</c> if not found.
    /// </returns>
    ValueTask<T> FindByIdAsync(string id);

    /// <summary>
    /// Asynchronously creates a new model with optional additional data.
    /// </summary>
    /// <param name="data">Optional additional data associated with the model. Defaults to <c>null</c>.</param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation.
    /// The result is the newly created model.
    /// </returns>
    ValueTask<T> NewAsync(JsonNode data = null);

    /// <summary>
    /// Asynchronously retrieves a list of all models.
    /// </summary>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation.
    /// The result is an <see cref="IEnumerable{T}"/> containing all models.
    /// </returns>
    ValueTask<IEnumerable<T>> GetAllAsync();

    /// <summary>
    /// Asynchronously retrieves a paginated list of models.
    /// </summary>
    /// <typeparam name="TQuery">The type of the query context used for filtering, sorting, and other query options.</typeparam>
    /// <param name="page">The page number of the results to retrieve.</param>
    /// <param name="pageSize">The number of results per page.</param>
    /// <param name="context">The query context containing filtering, sorting, and other parameters.</param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation.
    /// The result is a <see cref="PageResult{T}"/> containing the paginated query results.
    /// </returns>
    ValueTask<PageResult<T>> PageAsync<TQuery>(int page, int pageSize, TQuery context)
        where TQuery : QueryContext;

    /// <summary>
    /// Asynchronously creates the given model.
    /// </summary>
    /// <param name="model">The model to be created.</param>
    /// <returns>
    /// A <see cref="ValueTask"/> that represents the asynchronous operation. No result is returned.
    /// </returns>
    ValueTask CreateAsync(T model);

    /// <summary>
    /// Asynchronously updates the specified model with optional additional data.
    /// </summary>
    /// <param name="model">The model to be updated.</param>
    /// <param name="data">Optional additional data to update the model with. Defaults to <c>null</c>.</param>
    /// <returns>
    /// A <see cref="ValueTask"/> that represents the asynchronous operation. No result is returned.
    /// </returns>
    ValueTask UpdateAsync(T model, JsonNode data = null);

    /// <summary>
    /// Asynchronously validates the specified model.
    /// </summary>
    /// <param name="model">The model to be validated.</param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation.
    /// The result is a <see cref="ValidationResultDetails"/> indicating whether the model is valid.
    /// </returns>
    ValueTask<ValidationResultDetails> ValidateAsync(T model);
}
