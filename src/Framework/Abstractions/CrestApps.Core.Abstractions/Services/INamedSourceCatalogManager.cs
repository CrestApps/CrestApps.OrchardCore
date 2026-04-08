namespace CrestApps.Core.Services;

/// <summary>
/// A catalog manager that supports composite lookup by both name and source,
/// extending <see cref="INamedCatalogManager{T}"/> and <see cref="ISourceCatalogManager{T}"/>
/// for models that implement both <see cref="INameAwareModel"/> and <see cref="ISourceAwareModel"/>.
/// </summary>
/// <typeparam name="T">The type of catalog entry.</typeparam>
public interface INamedSourceCatalogManager<T> : INamedCatalogManager<T>, ISourceCatalogManager<T>
    where T : INameAwareModel, ISourceAwareModel
{
    /// <summary>
    /// Asynchronously retrieves a catalog entry by its unique name and source combination.
    /// </summary>
    /// <param name="name">The unique name of the entry.</param>
    /// <param name="source">The source or provider name of the entry.</param>
    /// <returns>The matching entry, or <see langword="null"/> if not found.</returns>
    ValueTask<T> GetAsync(string name, string source);
}
