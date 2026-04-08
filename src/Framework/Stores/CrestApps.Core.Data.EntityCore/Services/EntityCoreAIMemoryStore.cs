using CrestApps.Core.AI.Memory;
using CrestApps.Core.AI.Models;
using Microsoft.EntityFrameworkCore;

namespace CrestApps.Core.Data.EntityCore.Services;

public sealed class EntityCoreAIMemoryStore : DocumentCatalog<AIMemoryEntry>, IAIMemoryStore
{
    public EntityCoreAIMemoryStore(CrestAppsEntityDbContext dbContext)
        : base(dbContext)
    {
    }

    public Task<int> CountByUserAsync(string userId)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        return GetReadQuery()
            .Where(x => x.UserId == userId)
            .CountAsync();
    }

    public async Task<AIMemoryEntry> FindByUserAndNameAsync(string userId, string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        ArgumentException.ThrowIfNullOrEmpty(name);

        var record = await GetReadQuery()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Name == name);

        return record is null ? null : CatalogRecordFactory.Materialize<AIMemoryEntry>(record);
    }

    public async Task<IReadOnlyCollection<AIMemoryEntry>> GetByUserAsync(string userId, int limit = 100)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var records = await GetReadQuery()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.UpdatedUtc)
            .ThenByDescending(x => x.CreatedUtc)
            .Take(limit)
            .ToListAsync();

        return records
            .Select(CatalogRecordFactory.Materialize<AIMemoryEntry>)
            .ToArray();
    }
}
