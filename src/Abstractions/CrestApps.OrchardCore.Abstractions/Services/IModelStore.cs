namespace CrestApps.OrchardCore.Services;

public interface IModelStore<T> : IReadModelStore<T>
{
    /// <summary>
    /// Asynchronously deletes the specified model from the store.
    /// </summary>
    /// <param name="model">The model to delete. Must not be <c>null</c>.</param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> representing the asynchronous operation.
    /// The result is <c>true</c> if the deletion was successful, <c>false</c> if the model does not exist or could not be deleted.
    /// </returns>
    ValueTask<bool> DeleteAsync(T model);

    /// <summary>
    /// Asynchronously creates the specified model in the store.
    /// </summary>
    /// <param name="model">The model to create. Must not be <c>null</c>.</param>
    /// <returns>
    /// A <see cref="ValueTask"/> representing the asynchronous operation. No result is returned.
    /// </returns>
    ValueTask CreateAsync(T model);

    /// <summary>
    /// Asynchronously updates the specified model in the store.
    /// </summary>
    /// <param name="model">The model to update. Must not be <c>null</c>.</param>
    /// <returns>
    /// A <see cref="ValueTask"/> representing the asynchronous operation. No result is returned.
    /// </returns>
    ValueTask UpdateAsync(T model);

    /// <summary>
    /// Asynchronously saves all pending changes in the store.
    /// </summary>
    /// <returns>
    /// A <see cref="ValueTask"/> representing the asynchronous operation. No result is returned.
    /// </returns>
    ValueTask SaveChangesAsync();
}
