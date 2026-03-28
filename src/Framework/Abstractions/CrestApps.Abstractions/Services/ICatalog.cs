namespace CrestApps.Services;

public interface ICatalog<T> : IReadCatalog<T>
{
    /// <summary>
    /// Asynchronously deletes the specified entry from the catalog.
    /// </summary>
    ValueTask<bool> DeleteAsync(T entry);

    /// <summary>
    /// Asynchronously creates the specified entry in the catalog.
    /// </summary>
    ValueTask CreateAsync(T entry);

    /// <summary>
    /// Asynchronously updates the specified entry in the catalog.
    /// </summary>
    ValueTask UpdateAsync(T entry);

    /// <summary>
    /// Asynchronously saves all pending changes in the catalog.
    /// </summary>
    ValueTask SaveChangesAsync();
}
