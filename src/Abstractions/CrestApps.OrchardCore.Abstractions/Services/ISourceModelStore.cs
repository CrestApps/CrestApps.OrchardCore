using CrestApps.OrchardCore.Models;

namespace CrestApps.OrchardCore.Services;

public interface ISourceModelStore<T> : IModelStore<T>
    where T : ISourceAwareModel
{
    /// <summary>
    /// Asynchronously retrieves all models in the store with the given source.
    /// </summary>
    /// <returns>
    /// <param name="source">The source of the model. Must not be null or empty.</param>
    /// A ValueTask representing the asynchronous operation. The task result is an <see cref="IEnumerable{T}"/>
    /// containing all models available in the store.
    /// </returns>
    ValueTask<IEnumerable<T>> GetAsync(string source);
}
