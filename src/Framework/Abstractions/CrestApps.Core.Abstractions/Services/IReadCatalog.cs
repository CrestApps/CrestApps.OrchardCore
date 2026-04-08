using CrestApps.Core.Models;

namespace CrestApps.Core.Services;

/// <summary>
/// Provides read-only access to catalog entries, supporting retrieval by ID,
/// bulk retrieval, and paginated queries.
/// </summary>
/// <typeparam name="T">The type of catalog entry.</typeparam>
public interface IReadCatalog<T>
{
    /// <summary>
    /// Asynchronously finds a catalog entry by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the entry.</param>
    /// <returns>The matching entry, or <see langword="null"/> if not found.</returns>
    ValueTask<T> FindByIdAsync(string id);

    /// <summary>
    /// Asynchronously retrieves all entries in the catalog.
    /// </summary>
    /// <returns>A read-only collection of all catalog entries.</returns>
    ValueTask<IReadOnlyCollection<T>> GetAllAsync();

    /// <summary>
    /// Asynchronously retrieves catalog entries matching the specified identifiers.
    /// </summary>
    /// <param name="ids">The identifiers of the entries to retrieve.</param>
    /// <returns>A read-only collection of matching entries.</returns>
    ValueTask<IReadOnlyCollection<T>> GetAsync(IEnumerable<string> ids);

    /// <summary>
    /// Asynchronously retrieves a paginated subset of catalog entries using the specified query context.
    /// </summary>
    /// <typeparam name="TQuery">The query context type used to filter and order results.</typeparam>
    /// <param name="page">The one-based page number to retrieve.</param>
    /// <param name="pageSize">The number of entries per page.</param>
    /// <param name="context">The query context used to filter and order results.</param>
    /// <returns>A page result containing the entries and total count.</returns>
    ValueTask<PageResult<T>> PageAsync<TQuery>(int page, int pageSize, TQuery context)
        where TQuery : QueryContext;
}
