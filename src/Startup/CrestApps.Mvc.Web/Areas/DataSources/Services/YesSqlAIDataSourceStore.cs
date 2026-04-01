using CrestApps.AI.DataSources;
using CrestApps.AI.Models;
using CrestApps.Models;
using CrestApps.Mvc.Web.Indexes;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.Mvc.Web.Services;

public sealed class YesSqlAIDataSourceStore : IAIDataSourceStore
{
    private readonly ISession _session;

    public YesSqlAIDataSourceStore(ISession session)
    {
        _session = session;
    }

    public async ValueTask<AIDataSource> FindByIdAsync(string id)
    {
        return await _session.Query<AIDataSource, AIDataSourceIndex>(x => x.ItemId == id)
            .FirstOrDefaultAsync();
    }

    public async ValueTask<IReadOnlyCollection<AIDataSource>> GetAsync(IEnumerable<string> ids)
    {
        var items = await _session.Query<AIDataSource, AIDataSourceIndex>(x => x.ItemId.IsIn(ids))
            .ListAsync();

        return items.ToArray();
    }

    public async ValueTask<IReadOnlyCollection<AIDataSource>> GetAllAsync()
    {
        var items = await _session.Query<AIDataSource, AIDataSourceIndex>().ListAsync();

        return items.ToArray();
    }

    public async ValueTask<PageResult<AIDataSource>> PageAsync<TQuery>(int page, int pageSize, TQuery context)
        where TQuery : QueryContext
    {
        var query = _session.Query<AIDataSource, AIDataSourceIndex>();
        var skip = (page - 1) * pageSize;

        return new PageResult<AIDataSource>
        {
            Count = await query.CountAsync(),
            Entries = (await query.Skip(skip).Take(pageSize).ListAsync()).ToArray(),
        };
    }

    public async ValueTask CreateAsync(AIDataSource record)
    {
        if (string.IsNullOrEmpty(record.ItemId))
        {
            record.ItemId = UniqueId.GenerateId();
        }

        await _session.SaveAsync(record);
    }

    public async ValueTask UpdateAsync(AIDataSource record)
    {
        await _session.SaveAsync(record);
    }

    public ValueTask<bool> DeleteAsync(AIDataSource entry)
    {
        _session.Delete(entry);

        return ValueTask.FromResult(true);
    }

    public async ValueTask SaveChangesAsync()
    {
        await _session.SaveChangesAsync();
    }
}
