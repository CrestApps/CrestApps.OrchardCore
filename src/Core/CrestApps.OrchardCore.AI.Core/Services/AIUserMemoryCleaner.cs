using CrestApps.Core.AI.Memory;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Clears everything the AI has remembered about one user, shared by the user memory page and the AI tools. Each
/// memory is deleted through the catalog manager, so the memory index handlers remove it from the index too.
/// </summary>
public static class AIUserMemoryCleaner
{
    /// <summary>
    /// Deletes every memory of a user.
    /// </summary>
    /// <param name="memoryStore">The memory store.</param>
    /// <param name="memoryManager">The memory manager, which runs the memory handlers.</param>
    /// <param name="userId">The ID of the user.</param>
    /// <returns>How many memories the user had and how many were removed.</returns>
    public static async Task<AIUserMemoryClearResult> ClearAsync(IAIMemoryStore memoryStore, ICatalogManager<AIMemoryEntry> memoryManager, string userId)
    {
        ArgumentNullException.ThrowIfNull(memoryStore);
        ArgumentNullException.ThrowIfNull(memoryManager);

        if (await memoryStore.CountByUserAsync(userId) == 0)
        {
            return new AIUserMemoryClearResult(0, 0);
        }

        var memories = (await memoryStore.GetAllAsync())
            .Where(memory => string.Equals(memory.UserId, userId, StringComparison.Ordinal))
            .ToArray();

        var removed = 0;

        foreach (var memory in memories)
        {
            if (await memoryManager.DeleteAsync(memory))
            {
                removed++;
            }
        }

        return new AIUserMemoryClearResult(memories.Length, removed);
    }
}

/// <summary>
/// What clearing a user's AI memory did.
/// </summary>
/// <param name="Found">How many memories the user had.</param>
/// <param name="Removed">How many of them were removed.</param>
public sealed record AIUserMemoryClearResult(int Found, int Removed);
