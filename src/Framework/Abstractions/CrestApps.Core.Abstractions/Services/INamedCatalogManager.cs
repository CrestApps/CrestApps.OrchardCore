namespace CrestApps.Core.Services;

/// <summary>
/// A catalog manager that supports finding entries by their unique name,
/// extending <see cref="ICatalogManager{T}"/> with name-based lookup for models
/// that implement <see cref="INameAwareModel"/>.
/// </summary>
/// <typeparam name="T">The type of catalog entry, which must have a <see cref="INameAwareModel.Name"/> property.</typeparam>
public interface INamedCatalogManager<T> : ICatalogManager<T>
    where T : INameAwareModel
{
    /// <summary>
    /// Asynchronously finds a catalog entry by its unique name.
    /// </summary>
    /// <param name="name">The unique name of the entry to find.</param>
    /// <returns>The matching entry, or <see langword="null"/> if no entry with the specified name exists.</returns>
    ValueTask<T> FindByNameAsync(string name);
}
