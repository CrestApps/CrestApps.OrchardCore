using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;

namespace CrestApps.Core.AI.Memory;

/// <summary>
/// Provides persistent storage for AI memory entries, supporting per-user CRUD
/// operations and lookup. Memory entries allow AI sessions to recall user-specific
/// facts across conversations.
/// </summary>
public interface IAIMemoryStore : ICatalog<AIMemoryEntry>
{
    /// <summary>
    /// Asynchronously counts the number of memory entries owned by the specified user.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <returns>The total number of memory entries for the user.</returns>
    Task<int> CountByUserAsync(string userId);

    /// <summary>
    /// Asynchronously finds a memory entry by the owning user and entry name.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="name">The unique name of the memory entry within the user scope.</param>
    /// <returns>The matching entry, or <see langword="null"/> if not found.</returns>
    Task<AIMemoryEntry> FindByUserAndNameAsync(string userId, string name);

    /// <summary>
    /// Asynchronously retrieves memory entries owned by the specified user, up to the given limit.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="limit">The maximum number of entries to return. Defaults to 100.</param>
    /// <returns>A read-only collection of the user's memory entries.</returns>
    Task<IReadOnlyCollection<AIMemoryEntry>> GetByUserAsync(string userId, int limit = 100);
}
