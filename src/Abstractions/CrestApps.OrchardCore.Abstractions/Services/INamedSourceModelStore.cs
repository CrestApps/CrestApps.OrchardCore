using CrestApps.OrchardCore.Models;

namespace CrestApps.OrchardCore.Services;

public interface INamedSourceModelStore<T> : INamedModelStore<T>, ISourceModelStore<T>
    where T : INameAwareModel, ISourceAwareModel
{
    /// <summary>
    /// Asynchronously finds a model by its unique name and source.
    /// </summary>
    /// <param name="name">The unique name of the model. Must not be null or empty.</param>
    /// <param name="source">The source of the model. Must not be null or empty.</param>
    /// <returns>
    /// A ValueTask representing the asynchronous operation. The task result is the <see cref="T"/> if found,
    /// or <c>null</c> if no model with the specified name exists in the store.
    /// </returns>
    ValueTask<T> GetAsync(string name, string source);
}
