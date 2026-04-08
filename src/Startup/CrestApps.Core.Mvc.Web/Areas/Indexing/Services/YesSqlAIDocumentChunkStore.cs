using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Models;
using CrestApps.Core.Mvc.Web.Areas.Indexing.Indexes;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.Core.Mvc.Web.Areas.Indexing.Services;

public sealed class YesSqlAIDocumentChunkStore : IAIDocumentChunkStore
{
    private readonly ISession _session;

    public YesSqlAIDocumentChunkStore(ISession session)
    {
        _session = session;
    }

    public async Task<IReadOnlyCollection<AIDocumentChunk>> GetChunksByAIDocumentIdAsync(string documentId)
    {
        var chunks = await _session.Query<AIDocumentChunk, AIDocumentChunkIndex>(x =>
        x.AIDocumentId == documentId).ListAsync();

        return chunks.ToArray();
    }

    public async Task<IReadOnlyCollection<AIDocumentChunk>> GetChunksByReferenceAsync(string referenceId, string referenceType)
    {
        var chunks = await _session.Query<AIDocumentChunk, AIDocumentChunkIndex>(x =>
        x.ReferenceId == referenceId && x.ReferenceType == referenceType).ListAsync();

        return chunks.ToArray();
    }

    public async Task DeleteByDocumentIdAsync(string documentId)
    {
        var chunks = await _session.Query<AIDocumentChunk, AIDocumentChunkIndex>(x =>
        x.AIDocumentId == documentId).ListAsync();

        foreach (var chunk in chunks)
        {
            _session.Delete(chunk);
        }
    }

    public async ValueTask<AIDocumentChunk> FindByIdAsync(string id)
    {
        return await _session.Query<AIDocumentChunk, AIDocumentChunkIndex>(x => x.ItemId == id).FirstOrDefaultAsync();
    }

    public async ValueTask<IReadOnlyCollection<AIDocumentChunk>> GetAsync(IEnumerable<string> ids)
    {
        var items = await _session.Query<AIDocumentChunk, AIDocumentChunkIndex>(x => x.ItemId.IsIn(ids)).ListAsync();

        return items.ToArray();
    }

    public async ValueTask<IReadOnlyCollection<AIDocumentChunk>> GetAllAsync()
    {
        var items = await _session.Query<AIDocumentChunk, AIDocumentChunkIndex>().ListAsync();

        return items.ToArray();
    }

    public async ValueTask<PageResult<AIDocumentChunk>> PageAsync<TQuery>(int page, int pageSize, TQuery context)
        where TQuery : QueryContext
    {
        var query = _session.Query<AIDocumentChunk, AIDocumentChunkIndex>();
        var skip = (page - 1) * pageSize;

        return new PageResult<AIDocumentChunk>
        {
            Count = await query.CountAsync(),
            Entries = (await query.Skip(skip).Take(pageSize).ListAsync()).ToArray(),
        };
    }

    public async ValueTask CreateAsync(AIDocumentChunk record)
    {
        if (string.IsNullOrEmpty(record.ItemId))
        {
            record.ItemId = UniqueId.GenerateId();
        }

        await _session.SaveAsync(record);
    }

    public async ValueTask UpdateAsync(AIDocumentChunk record)
    {
        await _session.SaveAsync(record);
    }

    public ValueTask<bool> DeleteAsync(AIDocumentChunk entry)
    {
        _session.Delete(entry);

        return ValueTask.FromResult(true);
    }

}
