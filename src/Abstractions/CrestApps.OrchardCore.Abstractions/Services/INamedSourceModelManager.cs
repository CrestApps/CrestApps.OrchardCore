using CrestApps.OrchardCore.Models;

namespace CrestApps.OrchardCore.Services;

public interface INamedSourceModelManager<T> : INamedModelManager<T>, ISourceModelManager<T>
    where T : INameAwareModel, ISourceAwareModel
{
    /// <summary>
    /// Asynchronously retrieves a model for the specified name and source.
    /// </summary>
    /// <param name="name">The name of the model. Must not be null or empty.</param>
    /// <param name="source">The name of the provider. Must not be null or empty.</param>
    /// <returns>
    /// A ValueTask that represents the asynchronous operation. The result is an <see cref="T"/>
    /// containing the model deployments for the specified provider.
    /// </returns>
    ValueTask<T> GetAsync(string name, string source);
}
