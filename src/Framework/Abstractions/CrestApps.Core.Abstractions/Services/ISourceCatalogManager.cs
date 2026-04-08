using System.Text.Json.Nodes;

namespace CrestApps.Core.Services;

/// <summary>
/// A catalog manager that supports source-scoped creation and filtering,
/// extending <see cref="ICatalogManager{T}"/> for models that implement <see cref="ISourceAwareModel"/>.
/// </summary>
/// <typeparam name="T">The type of catalog entry, which must have a <see cref="ISourceAwareModel.Source"/> property.</typeparam>
public interface ISourceCatalogManager<T> : ICatalogManager<T>
    where T : ISourceAwareModel
{
    /// <summary>
    /// Asynchronously creates a new model instance pre-assigned to the specified source,
    /// optionally populating it from JSON data.
    /// </summary>
    /// <param name="source">The source or provider name to assign to the new model.</param>
    /// <param name="data">Optional JSON data to seed the new model.</param>
    /// <returns>A newly created and initialized model instance assigned to the specified source.</returns>
    ValueTask<T> NewAsync(string source, JsonNode data = null);

    /// <summary>
    /// Asynchronously retrieves all catalog entries belonging to the specified source.
    /// </summary>
    /// <param name="source">The source or provider name to filter by.</param>
    /// <returns>An enumerable of entries matching the specified source.</returns>
    ValueTask<IEnumerable<T>> GetAsync(string source);

    /// <summary>
    /// Asynchronously finds all catalog entries that belong to the specified source.
    /// </summary>
    /// <param name="source">The source or provider name to search for.</param>
    /// <returns>An enumerable of entries matching the specified source.</returns>
    ValueTask<IEnumerable<T>> FindBySourceAsync(string source);
}
