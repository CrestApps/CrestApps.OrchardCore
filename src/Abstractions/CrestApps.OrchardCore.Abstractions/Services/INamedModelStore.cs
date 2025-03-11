namespace CrestApps.OrchardCore.Services;

public interface INamedModelStore<T> : IModelStore<T>
    where T : INameAwareModel
{
    /// <summary>
    /// Asynchronously retrieves a model by its unique name.
    /// </summary>
    /// <param name="name">The unique name of the model. Must not be <c>null</c> or empty.</param>
    /// <returns>
    /// A <see cref="ValueTask{T}"/> representing the asynchronous operation.
    /// The result is the model if found, or <c>null</c> if no model with the specified name exists in the store.
    /// </returns>
    ValueTask<T> FindByNameAsync(string name);
}
