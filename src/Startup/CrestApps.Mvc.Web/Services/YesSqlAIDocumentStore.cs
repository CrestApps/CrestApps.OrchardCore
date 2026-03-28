using CrestApps.AI;
using CrestApps.AI.Models;
using CrestApps.Models;
using CrestApps.Mvc.Web.Indexes;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.Mvc.Web.Services;

public sealed class YesSqlAIDocumentStore : IAIDocumentStore
{
    private readonly ISession _session;

    public YesSqlAIDocumentStore(ISession session)
    {
        _session = session;
    }

    public async Task<IReadOnlyCollection<AIDocument>> GetDocumentsAsync(string referenceId, string referenceType)
    {
        var docs = await _session.Query<AIDocument, AIDocumentIndex>(x =>
            x.ReferenceId == referenceId && x.ReferenceType == referenceType).ListAsync();

        return docs.ToArray();
    }

    public async ValueTask<AIDocument> FindByIdAsync(string id)
    {
        return await _session.Query<AIDocument, AIDocumentIndex>(x => x.ItemId == id).FirstOrDefaultAsync();
    }

    public async ValueTask<IReadOnlyCollection<AIDocument>> GetAsync(IEnumerable<string> ids)
    {
        var items = await _session.Query<AIDocument, AIDocumentIndex>(x => x.ItemId.IsIn(ids)).ListAsync();

        return items.ToArray();
    }

    public async ValueTask<IReadOnlyCollection<AIDocument>> GetAllAsync()
    {
        var items = await _session.Query<AIDocument, AIDocumentIndex>().ListAsync();

        return items.ToArray();
    }

    public async ValueTask<PageResult<AIDocument>> PageAsync<TQuery>(int page, int pageSize, TQuery context)
        where TQuery : QueryContext
    {
        var query = _session.Query<AIDocument, AIDocumentIndex>();
        var skip = (page - 1) * pageSize;

        return new PageResult<AIDocument>
        {
            Count = await query.CountAsync(),
            Entries = (await query.Skip(skip).Take(pageSize).ListAsync()).ToArray(),
        };
    }

    public async ValueTask CreateAsync(AIDocument record)
    {
        if (string.IsNullOrEmpty(record.ItemId))
        {
            record.ItemId = UniqueId.GenerateId();
        }

        await _session.SaveAsync(record);
    }

    public async ValueTask UpdateAsync(AIDocument record)
    {
        await _session.SaveAsync(record);
    }

    public ValueTask<bool> DeleteAsync(AIDocument entry)
    {
        _session.Delete(entry);

        return ValueTask.FromResult(true);
    }

    public async ValueTask SaveChangesAsync()
    {
        await _session.SaveChangesAsync();
    }
}
