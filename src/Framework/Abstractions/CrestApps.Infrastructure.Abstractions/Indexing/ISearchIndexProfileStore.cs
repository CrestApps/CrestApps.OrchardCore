using CrestApps.Infrastructure.Indexing.Models;
using CrestApps.Services;

namespace CrestApps.Infrastructure.Indexing;

/// <summary>
/// Store for managing <see cref="SearchIndexProfile"/> records.
/// </summary>
public interface ISearchIndexProfileStore : ICatalog<SearchIndexProfile>
{
    /// <summary>
    /// Finds an index profile by its unique name.
    /// </summary>
    /// <param name="name">The unique name of the index profile.</param>
    /// <returns>The matching index profile, or <see langword="null"/> if not found.</returns>
    Task<SearchIndexProfile> FindByNameAsync(string name);

    /// <summary>
    /// Gets all index profiles of the specified type (e.g., "AIDocuments", "DataSourceIndex", "AIMemory").
    /// </summary>
    /// <param name="type">The index profile type to filter by.</param>
    /// <returns>A read-only collection of matching index profiles.</returns>
    Task<IReadOnlyCollection<SearchIndexProfile>> GetByTypeAsync(string type);
}
