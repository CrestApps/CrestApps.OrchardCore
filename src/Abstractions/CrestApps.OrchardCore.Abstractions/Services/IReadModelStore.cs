using CrestApps.OrchardCore.Models;

namespace CrestApps.OrchardCore.Services;

public interface IReadModelStore<T>
{
    /// <summary>
    /// Asynchronously retrieves a model by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the model. Must not be <c>null</c> or empty.</param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> representing the asynchronous operation.
    /// The result is the model if found, or <c>null</c> if no matching model exists in the store.
    /// </returns>
    ValueTask<T> FindByIdAsync(string id);

    /// <summary>
    /// Asynchronously retrieves all models from the store.
    /// </summary>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> representing the asynchronous operation.
    /// The result is an <see cref="IEnumerable{T}"/> containing all models in the store.
    /// </returns>
    ValueTask<IEnumerable<T>> GetAllAsync();

    /// <summary>
    /// Asynchronously retrieves a paginated list of models based on the specified pagination and filtering parameters.
    /// </summary>
    /// <typeparam name="TQuery">The type of the query context used for filtering, sorting, and other query options.</typeparam>
    /// <param name="page">The page number to retrieve (1-based index).</param>
    /// <param name="pageSize">The number of models to retrieve per page.</param>
    /// <param name="context">The query context containing filtering, sorting, and search parameters. Can be <c>null</c>.</param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> representing the asynchronous operation.
    /// The result is a <see cref="PageResult{T}"/> containing the models for the requested page, along with pagination metadata.
    /// </returns>
    ValueTask<PageResult<T>> PageAsync<TQuery>(int page, int pageSize, TQuery context)
        where TQuery : QueryContext;
}
