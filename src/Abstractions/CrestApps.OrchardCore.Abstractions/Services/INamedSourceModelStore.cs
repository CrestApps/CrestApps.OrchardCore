namespace CrestApps.OrchardCore.Services;

public interface INamedSourceModelStore<T> : INamedModelStore<T>, ISourceModelStore<T>
    where T : INameAwareModel, ISourceAwareModel
{
    /// <summary>
    /// Asynchronously retrieves a model by its unique name and source.
    /// </summary>
    /// <param name="name">The unique name of the model. Must not be <c>null</c> or empty.</param>
    /// <param name="source">The source of the model. Must not be <c>null</c> or empty.</param>
    /// <returns>
    /// A <see cref="ValueTask{T}"/> representing the asynchronous operation.
    /// The result is the model if found, or <c>null</c> if no model with the specified name and source exists in the store.
    /// </returns>
    ValueTask<T> GetAsync(string name, string source);
}
