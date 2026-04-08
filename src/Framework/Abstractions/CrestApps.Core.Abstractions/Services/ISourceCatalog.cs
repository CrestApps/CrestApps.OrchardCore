namespace CrestApps.Core.Services;

/// <summary>
/// A catalog that supports filtering entries by their source or provider name,
/// extending <see cref="ICatalog{T}"/> for models that implement <see cref="ISourceAwareModel"/>.
/// </summary>
/// <typeparam name="T">The type of catalog entry, which must have a <see cref="ISourceAwareModel.Source"/> property.</typeparam>
public interface ISourceCatalog<T> : ICatalog<T>
    where T : ISourceAwareModel
{
    /// <summary>
    /// Asynchronously retrieves all catalog entries belonging to the specified source.
    /// </summary>
    /// <param name="source">The source or provider name to filter by.</param>
    /// <returns>A read-only collection of entries matching the specified source.</returns>
    ValueTask<IReadOnlyCollection<T>> GetAsync(string source);
}
