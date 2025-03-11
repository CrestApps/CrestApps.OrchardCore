using CrestApps.OrchardCore.Models;

namespace CrestApps.OrchardCore.Services;

public interface IModelStore<T>
{
    /// <summary>
    /// Asynchronously deletes a specified model from the store.
    /// </summary>
    /// <param name="model">The model to delete. Must not be null.</param>
    /// <returns>
    /// A ValueTask representing the asynchronous operation. The task result is a boolean indicating
    /// whether the model was successfully deleted. Returns <c>true</c> if the deletion succeeded,
    /// and <c>false</c> if the model could not be deleted (e.g., if it does not exist).
    /// </returns>
    ValueTask<bool> DeleteAsync(T model);

    /// <summary>
    /// Asynchronously finds a model by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier (ID) of the model. Must not be null or empty.</param>
    /// <returns>
    /// A ValueTask representing the asynchronous operation. The task result is the <see cref="T"/> if found,
    /// or <c>null</c> if no model with the specified identifier exists in the store.
    /// </returns>
    ValueTask<T> FindByIdAsync(string id);

    /// <summary>
    /// Asynchronously retrieves all models in the store.
    /// </summary>
    /// <returns>
    /// A ValueTask representing the asynchronous operation. The task result is an <see cref="IEnumerable{T}"/>
    /// containing all models available in the store.
    /// </returns>
    ValueTask<IEnumerable<T>> GetAllAsync();

    /// <summary>
    /// Asynchronously retrieves a paged list of models based on the specified pagination and filtering parameters.
    /// </summary>
    /// <param name="page">The page number to retrieve, where the index is 1-based.</param>
    /// <param name="pageSize">The number of models to retrieve per page.</param>
    /// <param name="context">The query context containing additional filtering, sorting, and search criteria. Can be null.</param>
    /// <returns>
    /// A ValueTask representing the asynchronous operation. The task result is a <see cref="PageResult<T>"/> object,
    /// which contains the list of model models for the requested page, along with metadata for pagination (such as total count, etc.).
    /// </returns>
    ValueTask<PageResult<T>> PageAsync<TQuery>(int page, int pageSize, TQuery context)
        where TQuery : QueryContext;

    /// <summary>
    /// Asynchronously creates the specified model in the store.
    /// </summary>
    /// <param name="model">The model to save or update. Must not be null.</param>
    /// <returns>
    /// A ValueTask representing the asynchronous operation. This method does not return any value.
    /// </returns>
    ValueTask CreateAsync(T model);

    /// <summary>
    /// Asynchronously updates the specified model in the store.
    /// </summary>
    /// <param name="model">The model to save or update. Must not be null.</param>
    /// <returns>
    /// A ValueTask representing the asynchronous operation. This method does not return any value.
    /// </returns>
    ValueTask UpdateAsync(T model);

    /// <summary>
    /// Asynchronously saves the changes in the store.
    /// </summary>
    /// <returns>
    /// A ValueTask representing the asynchronous operation. This method does not return any value.
    /// </returns>
    ValueTask SaveChangesAsync();
}
