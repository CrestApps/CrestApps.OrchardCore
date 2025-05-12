using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.Models;
using OrchardCore;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.Core.Services;

public class DefaultAIDataSourceStore : IAIDataSourceStore
{
    protected readonly IDocumentManager<ModelDocument<AIDataSource>> DocumentManager;

    public DefaultAIDataSourceStore(IDocumentManager<ModelDocument<AIDataSource>> documentManager)
    {
        DocumentManager = documentManager;
    }

    public async ValueTask<bool> DeleteAsync(AIDataSource model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var document = await DocumentManager.GetOrCreateMutableAsync();

        if (!document.Records.TryGetValue(model.Id, out var existingInstance))
        {
            return false;
        }

        var removed = document.Records.Remove(model.Id);

        if (removed)
        {
            await DocumentManager.UpdateAsync(document);
        }

        return removed;
    }

    public async ValueTask<AIDataSource> FindByIdAsync(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        var document = await DocumentManager.GetOrCreateImmutableAsync();

        if (document.Records.TryGetValue(id, out var record))
        {
            return record;
        }

        return null;
    }

    public async ValueTask<IEnumerable<AIDataSource>> GetAsync(string providerName)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerName);

        var document = await DocumentManager.GetOrCreateImmutableAsync();

        return document.Records.Values.Where(x => x.ProfileSource.Equals(providerName, StringComparison.OrdinalIgnoreCase));
    }

    public async ValueTask<IEnumerable<AIDataSource>> GetAsync(string providerName, string type)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerName);
        ArgumentException.ThrowIfNullOrEmpty(type);

        var document = await DocumentManager.GetOrCreateImmutableAsync();

        return document.Records.Values.Where(x => x.ProfileSource.Equals(providerName, StringComparison.OrdinalIgnoreCase) && x.Type.Equals(type, StringComparison.OrdinalIgnoreCase));
    }

    public async ValueTask<PageResult<AIDataSource>> PageAsync<TQuery>(int page, int pageSize, TQuery context)
            where TQuery : QueryContext
    {
        var records = await LocateInstancesAsync(context);

        var skip = (page - 1) * pageSize;

        return new PageResult<AIDataSource>
        {
            Count = records.Count(),
            Models = records.Skip(skip).Take(pageSize).ToArray()
        };
    }

    public async ValueTask<IEnumerable<AIDataSource>> GetAllAsync()
    {
        var document = await DocumentManager.GetOrCreateImmutableAsync();

        return document.Records.Values;
    }

    public async ValueTask CreateAsync(AIDataSource record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var document = await DocumentManager.GetOrCreateMutableAsync();

        if (string.IsNullOrEmpty(record.Id))
        {
            record.Id = IdGenerator.GenerateId();
        }

        document.Records[record.Id] = record;

        await DocumentManager.UpdateAsync(document);
    }

    public async ValueTask UpdateAsync(AIDataSource record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var document = await DocumentManager.GetOrCreateMutableAsync();

        if (string.IsNullOrEmpty(record.Id))
        {
            record.Id = IdGenerator.GenerateId();
        }

        document.Records[record.Id] = record;

        await DocumentManager.UpdateAsync(document);
    }

    public ValueTask SaveChangesAsync()
    {
        return ValueTask.CompletedTask;
    }

    protected virtual async ValueTask<IEnumerable<AIDataSource>> LocateInstancesAsync(QueryContext context)
    {
        var document = await DocumentManager.GetOrCreateImmutableAsync();

        if (context == null)
        {
            return document.Records.Values;
        }

        var records = document.Records.Values.AsEnumerable();

        records = GetSortable(context, records);

        return records;
    }

    protected virtual IEnumerable<AIDataSource> GetSortable(QueryContext context, IEnumerable<AIDataSource> records)
    {
        if (!string.IsNullOrEmpty(context.Name))
        {
            records = records.Where(x => context.Name.Contains(x.DisplayText, StringComparison.OrdinalIgnoreCase));
        }

        if (context.Sorted)
        {
            records = records.OrderBy(x => x.DisplayText);
        }

        return records;
    }
}
