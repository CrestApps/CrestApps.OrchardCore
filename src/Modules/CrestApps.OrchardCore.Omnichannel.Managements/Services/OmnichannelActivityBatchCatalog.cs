using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

public sealed class OmnichannelActivityBatchCatalog : ICatalog<OmnichannelActivityBatch>
{
    private readonly ISession _session;

    public OmnichannelActivityBatchCatalog(ISession session)
    {
        _session = session;
    }

    public async ValueTask CreateAsync(OmnichannelActivityBatch entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        await _session.SaveAsync(entry, OmnichannelConstants.CollectionName);
    }

    public ValueTask<bool> DeleteAsync(OmnichannelActivityBatch entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        _session.Delete(entry, OmnichannelConstants.CollectionName);

        return ValueTask.FromResult(true);
    }

    public async ValueTask<OmnichannelActivityBatch> FindByIdAsync(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        return await _session.Query<OmnichannelActivityBatch, OmnichannelActivityBatchIndex>(index => index.BatchId == id, collection: OmnichannelConstants.CollectionName)
            .FirstOrDefaultAsync();
    }

    public async ValueTask<IReadOnlyCollection<OmnichannelActivityBatch>> GetAllAsync()
    {
        return (await _session.Query<OmnichannelActivityBatch, OmnichannelActivityBatchIndex>(collection: OmnichannelConstants.CollectionName).ListAsync()).ToArray();
    }

    public async ValueTask<IReadOnlyCollection<OmnichannelActivityBatch>> GetAsync(IEnumerable<string> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var items = await _session.Query<OmnichannelActivityBatch, OmnichannelActivityBatchIndex>(index => index.BatchId.IsIn(ids), collection: OmnichannelConstants.CollectionName).ListAsync();

        return items.ToArray();
    }

    public async ValueTask<PageResult<OmnichannelActivityBatch>> PageAsync<TQuery>(int page, int pageSize, TQuery context) where TQuery : QueryContext
    {
        var query = _session.Query<OmnichannelActivityBatch, OmnichannelActivityBatchIndex>(collection: OmnichannelConstants.CollectionName);

        if (!string.IsNullOrEmpty(context.Name))
        {
            query = query.Where(x => x.DisplayText.Contains(context.Name));
        }

        if (context.Sorted)
        {
            query = query.OrderBy(x => x.DisplayText);
        }

        var skip = (page - 1) * pageSize;

        return new PageResult<OmnichannelActivityBatch>
        {
            Count = await query.CountAsync(),
            Entries = (await query.Skip(skip).Take(pageSize).ListAsync()).ToArray(),
        };
    }

    public async ValueTask SaveChangesAsync()
    {
        await _session.FlushAsync();
    }

    public async ValueTask UpdateAsync(OmnichannelActivityBatch entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        await _session.SaveAsync(entry, OmnichannelConstants.CollectionName);
    }
}
