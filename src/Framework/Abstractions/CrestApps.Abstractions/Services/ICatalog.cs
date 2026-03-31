namespace CrestApps.Services;

/// <summary>
/// Provides full CRUD operations for catalog entries, extending read-only access
/// with the ability to create, update, delete, and persist changes.
/// </summary>
/// <typeparam name="T">The type of catalog entry.</typeparam>
public interface ICatalog<T> : IReadCatalog<T>
{
    /// <summary>
    /// Asynchronously deletes the specified entry from the catalog.
    /// </summary>
    /// <param name="entry">The entry to delete.</param>
    /// <returns><see langword="true"/> if the entry was successfully deleted; otherwise, <see langword="false"/>.</returns>
    ValueTask<bool> DeleteAsync(T entry);

    /// <summary>
    /// Asynchronously creates the specified entry in the catalog.
    /// </summary>
    /// <param name="entry">The entry to create.</param>
    ValueTask CreateAsync(T entry);

    /// <summary>
    /// Asynchronously updates the specified entry in the catalog.
    /// </summary>
    /// <param name="entry">The entry to update.</param>
    ValueTask UpdateAsync(T entry);

    /// <summary>
    /// Asynchronously saves all pending changes in the catalog.
    /// </summary>
    ValueTask SaveChangesAsync();
}
