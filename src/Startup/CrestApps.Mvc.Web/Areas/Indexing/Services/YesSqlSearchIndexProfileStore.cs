using CrestApps.Infrastructure.Indexing;
using CrestApps.Infrastructure.Indexing.Models;
using CrestApps.Models;
using CrestApps.Mvc.Web.Indexes;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.Mvc.Web.Services;

public sealed class YesSqlSearchIndexProfileStore : ISearchIndexProfileStore
{
    private readonly ISession _session;
    public YesSqlSearchIndexProfileStore(ISession session)
    {
        _session = session;
    }

    public async Task<SearchIndexProfile> FindByNameAsync(string name)
    {
        return await _session.Query<SearchIndexProfile, SearchIndexProfileIndex>(x => x.Name == name)
            .FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyCollection<SearchIndexProfile>> GetByTypeAsync(string type)
    {
        var items = await _session.Query<SearchIndexProfile, SearchIndexProfileIndex>(x => x.Type == type)
            .ListAsync();

        return items.ToArray();
    }

    public async ValueTask<SearchIndexProfile> FindByIdAsync(string id)
    {
        return await _session.Query<SearchIndexProfile, SearchIndexProfileIndex>(x => x.ItemId == id)
            .FirstOrDefaultAsync();
    }

    public async ValueTask<IReadOnlyCollection<SearchIndexProfile>> GetAsync(IEnumerable<string> ids)
    {
        var items = await _session.Query<SearchIndexProfile, SearchIndexProfileIndex>(x => x.ItemId.IsIn(ids))
            .ListAsync();

        return items.ToArray();
    }

    public async ValueTask<IReadOnlyCollection<SearchIndexProfile>> GetAllAsync()
    {
        var items = await _session.Query<SearchIndexProfile, SearchIndexProfileIndex>().ListAsync();

        return items.ToArray();
    }

    public async ValueTask<PageResult<SearchIndexProfile>> PageAsync<TQuery>(int page, int pageSize, TQuery context)
        where TQuery : QueryContext
    {
        var query = _session.Query<SearchIndexProfile, SearchIndexProfileIndex>();
        var skip = (page - 1) * pageSize;

        return new PageResult<SearchIndexProfile>
        {
            Count = await query.CountAsync(),
            Entries = (await query.Skip(skip).Take(pageSize).ListAsync()).ToArray(),
        };
    }

    public async ValueTask CreateAsync(SearchIndexProfile record)
    {
        if (string.IsNullOrEmpty(record.ItemId))
        {
            record.ItemId = UniqueId.GenerateId();
        }

        await _session.SaveAsync(record);
    }

    public async ValueTask UpdateAsync(SearchIndexProfile record)
    {
        await _session.SaveAsync(record);
    }

    public ValueTask<bool> DeleteAsync(SearchIndexProfile entry)
    {
        _session.Delete(entry);

        return ValueTask.FromResult(true);
    }

    public async ValueTask SaveChangesAsync()
    {
        await _session.SaveChangesAsync();
    }
}
