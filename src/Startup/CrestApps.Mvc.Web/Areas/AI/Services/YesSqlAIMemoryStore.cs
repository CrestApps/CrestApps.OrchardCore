using CrestApps.AI.Memory;
using CrestApps.AI.Models;
using CrestApps.Models;
using CrestApps.Mvc.Web.Indexes;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.Mvc.Web.Services;

public sealed class YesSqlAIMemoryStore : IAIMemoryStore
{
    private readonly ISession _session;

    public YesSqlAIMemoryStore(ISession session)
    {
        _session = session;
    }

    public async Task<int> CountByUserAsync(string userId)
    {
        return await _session.Query<AIMemoryEntry, AIMemoryEntryIndex>(x => x.UserId == userId)
            .CountAsync();
    }

    public async Task<AIMemoryEntry> FindByUserAndNameAsync(string userId, string name)
    {
        return await _session.Query<AIMemoryEntry, AIMemoryEntryIndex>(x =>
        x.UserId == userId && x.Name == name).FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyCollection<AIMemoryEntry>> GetByUserAsync(string userId, int limit = 100)
    {
        var items = await _session.Query<AIMemoryEntry, AIMemoryEntryIndex>(x => x.UserId == userId)
            .Take(limit)
            .ListAsync();

        return items.ToArray();
    }

    public async ValueTask<AIMemoryEntry> FindByIdAsync(string id)
    {
        return await _session.Query<AIMemoryEntry, AIMemoryEntryIndex>(x => x.ItemId == id)
            .FirstOrDefaultAsync();
    }

    public async ValueTask<IReadOnlyCollection<AIMemoryEntry>> GetAsync(IEnumerable<string> ids)
    {
        var items = await _session.Query<AIMemoryEntry, AIMemoryEntryIndex>(x => x.ItemId.IsIn(ids))
            .ListAsync();

        return items.ToArray();
    }

    public async ValueTask<IReadOnlyCollection<AIMemoryEntry>> GetAllAsync()
    {
        var items = await _session.Query<AIMemoryEntry, AIMemoryEntryIndex>().ListAsync();

        return items.ToArray();
    }

    public async ValueTask<PageResult<AIMemoryEntry>> PageAsync<TQuery>(int page, int pageSize, TQuery context)
        where TQuery : QueryContext
    {
        var query = _session.Query<AIMemoryEntry, AIMemoryEntryIndex>();
        var skip = (page - 1) * pageSize;

        return new PageResult<AIMemoryEntry>
        {
            Count = await query.CountAsync(),
            Entries = (await query.Skip(skip).Take(pageSize).ListAsync()).ToArray(),
        };
    }

    public async ValueTask CreateAsync(AIMemoryEntry record)
    {
        if (string.IsNullOrEmpty(record.ItemId))
        {
            record.ItemId = UniqueId.GenerateId();
        }

        await _session.SaveAsync(record);
    }

    public async ValueTask UpdateAsync(AIMemoryEntry record)
    {
        await _session.SaveAsync(record);
    }

    public ValueTask<bool> DeleteAsync(AIMemoryEntry entry)
    {
        _session.Delete(entry);

        return ValueTask.FromResult(true);
    }

    public async ValueTask SaveChangesAsync()
    {
        await _session.SaveChangesAsync();
    }
}
