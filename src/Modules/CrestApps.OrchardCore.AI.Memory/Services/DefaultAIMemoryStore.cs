using CrestApps.Core.AI.Memory;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Data.YesSql.Indexes.AIMemory;
using CrestApps.OrchardCore.YesSql.Core.Services;
using Microsoft.AspNetCore.Identity;
using YesSql;

namespace CrestApps.OrchardCore.AI.Memory.Services;

public sealed class DefaultAIMemoryStore : DocumentCatalog<AIMemoryEntry, AIMemoryEntryIndex>, IAIMemoryStore
{
    private readonly ILookupNormalizer _lookupNormalizer;

    public DefaultAIMemoryStore(
        ISession session,
        ILookupNormalizer lookupNormalizer)
        : base(session)
    {
        _lookupNormalizer = lookupNormalizer;
        CollectionName = MemoryConstants.CollectionName;
    }

    public Task<int> CountByUserAsync(string userId)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        return Session.Query<AIMemoryEntry, AIMemoryEntryIndex>(
            x => x.UserId == userId,
            CollectionName).CountAsync();
    }

    public async Task<AIMemoryEntry> FindByUserAndNameAsync(string userId, string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        ArgumentException.ThrowIfNullOrEmpty(name);

        var normalizedName = _lookupNormalizer.NormalizeName(name);

        if (string.IsNullOrEmpty(normalizedName))
        {
            return null;
        }

        var entries = await Session.Query<AIMemoryEntry, AIMemoryEntryIndex>(
            x => x.UserId == userId,
            CollectionName)
            .ListAsync();

        return entries.FirstOrDefault(entry => _lookupNormalizer.NormalizeName(entry.Name) == normalizedName);
    }

    public async Task<IReadOnlyCollection<AIMemoryEntry>> GetByUserAsync(string userId, int limit = 100)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var query = Session.Query<AIMemoryEntry, AIMemoryEntryIndex>(
            x => x.UserId == userId,
            CollectionName);
        var entries = await query.ListAsync();
        IEnumerable<AIMemoryEntry> orderedEntries = entries.OrderByDescending(x => x.UpdatedUtc);

        if (limit > 0)
        {
            orderedEntries = orderedEntries.Take(limit);
        }

        return orderedEntries.ToArray();
    }
}
