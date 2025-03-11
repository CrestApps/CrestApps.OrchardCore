using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.Services;

public interface ISourceModelManager<T> : IModelManager<T>
    where T : ISourceAwareModel
{
    /// <summary>
    /// Asynchronously creates a new model with the given source and optional additional data.
    /// </summary>
    /// <param name="source">The source from which the model is created. Must not be <c>null</c> or empty.</param>
    /// <param name="data">Optional additional data associated with the model. Defaults to <c>null</c>.</param>
    /// <returns>
    /// A <see cref="ValueTask{T}"/> representing the asynchronous operation.
    /// The result is the newly created model.
    /// </returns>
    ValueTask<T> NewAsync(string source, JsonNode data = null);

    /// <summary>
    /// Asynchronously retrieves all models associated with the specified source.
    /// </summary>
    /// <param name="source">The source of the models. Must not be <c>null</c> or empty.</param>
    /// <returns>
    /// A <see cref="ValueTask{IEnumerable{T}}"/> representing the asynchronous operation.
    /// The result is a collection of models associated with the given source.
    /// </returns>
    ValueTask<IEnumerable<T>> GetAsync(string source);

    /// <summary>
    /// Asynchronously retrieves all models associated with the specified source.
    /// </summary>
    /// <param name="source">The unique identifier of the source. Must not be <c>null</c> or empty.</param>
    /// <returns>
    /// A <see cref="ValueTask{IEnumerable{T}}"/> representing the asynchronous operation.
    /// The result is a collection of models associated with the specified source.
    /// </returns>
    ValueTask<IEnumerable<T>> FindBySourceAsync(string source);
}
