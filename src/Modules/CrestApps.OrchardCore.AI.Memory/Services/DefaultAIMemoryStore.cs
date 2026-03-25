using CrestApps.OrchardCore.AI.Memory.Indexes;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using Microsoft.AspNetCore.Identity;
using YesSql;

namespace CrestApps.OrchardCore.AI.Memory.Services;

public sealed class DefaultAIMemoryStore : DocumentCatalog<AIMemoryEntry, AIMemoryEntryIndex>, IAIMemoryStore
{
    private readonly ILookupNormalizer _lookupNormalizer;

    public DefaultAIMemoryStore(ISession session, ILookupNormalizer lookupNormalizer)
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

        return await Session.Query<AIMemoryEntry, AIMemoryEntryIndex>(
            x => x.UserId == userId && x.NormalizedName == normalizedName,
            CollectionName)
            .FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyCollection<AIMemoryEntry>> GetByUserAsync(string userId, int limit = 100)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        IQuery<AIMemoryEntry> query = Session.Query<AIMemoryEntry, AIMemoryEntryIndex>(
            x => x.UserId == userId,
            CollectionName);

        query = query
            .With<AIMemoryEntryIndex>()
            .OrderByDescending(x => x.UpdatedUtc);

        if (limit > 0)
        {
            query = query.Take(limit);
        }

        return (await query.ListAsync()).ToArray();
    }
}
