using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Models;

namespace CrestApps.OrchardCore.Services;

public interface ISourceModelManager<T> : IModelManager<T>
    where T : ISourceAwareModel
{
    /// <summary>
    /// Asynchronously creates a new model with the given source and optional additional data.
    /// </summary>
    /// <param name="source">The source from which the model is created.</param>
    /// <param name="data">Optional additional data associated with the profile. Defaults to null.</param>
    /// <returns>A ValueTask that represents the asynchronous operation. The result is the newly created model.</returns>
    ValueTask<T> NewAsync(string source, JsonNode data = null);

    /// <summary>
    /// Asynchronously retrieves all models in the store with the given source.
    /// </summary>
    /// <returns>
    /// <param name="source">The source of the model. Must not be null or empty.</param>
    /// A ValueTask representing the asynchronous operation. The task result is an <see cref="IEnumerable{T}"/>
    /// containing all models available in the store.
    /// </returns>
    ValueTask<IEnumerable<T>> GetAsync(string source);

    /// <summary>
    /// Asynchronously retrieves a list of models for the specified provider.
    /// </summary>
    /// <param name="source">The name of the provider. Must not be null or empty.</param>
    /// <returns>
    /// A ValueTask that represents the asynchronous operation. The result is an <see cref="IEnumerable{T}"/>
    /// containing the model deployments for the specified provider.
    /// </returns>
    ValueTask<IEnumerable<T>> FindBySourceAsync(string source);
}
