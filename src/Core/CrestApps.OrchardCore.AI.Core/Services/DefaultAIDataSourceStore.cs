using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Models;
using OrchardCore;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class DefaultAIDataSourceStore : IAIDataSourceStore
{
    private readonly IDocumentManager<DictionaryDocument<AIDataSource>> _documentManager;

    public DefaultAIDataSourceStore(IDocumentManager<DictionaryDocument<AIDataSource>> documentManager)
    {
        _documentManager = documentManager;
    }

    public async ValueTask<bool> DeleteAsync(AIDataSource model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var document = await _documentManager.GetOrCreateMutableAsync();

        if (!document.Records.TryGetValue(model.Id, out var existingInstance))
        {
            return false;
        }

        var removed = document.Records.Remove(model.Id);

        if (removed)
        {
            await _documentManager.UpdateAsync(document);
        }

        return removed;
    }

    public async ValueTask<AIDataSource> FindByIdAsync(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        var document = await _documentManager.GetOrCreateImmutableAsync();

        if (document.Records.TryGetValue(id, out var record))
        {
            return record.Clone();
        }

        return null;
    }

    public async ValueTask<IReadOnlyCollection<AIDataSource>> GetAsync(string providerName)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerName);

        var document = await _documentManager.GetOrCreateImmutableAsync();

        return document.Records.Values
            .Where(x => x.ProfileSource.Equals(providerName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    public async ValueTask<IReadOnlyCollection<AIDataSource>> GetAsync(string providerName, string type)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerName);
        ArgumentException.ThrowIfNullOrEmpty(type);

        var document = await _documentManager.GetOrCreateImmutableAsync();

        return document.Records.Values
            .Where(x => x.ProfileSource.Equals(providerName, StringComparison.OrdinalIgnoreCase) && x.Type.Equals(type, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    public async ValueTask<PageResult<AIDataSource>> PageAsync<TQuery>(int page, int pageSize, TQuery context)
            where TQuery : QueryContext
    {
        var records = await LocateInstancesAsync(context);

        var skip = (page - 1) * pageSize;

        return new PageResult<AIDataSource>
        {
            Count = records.Count,
            Entries = records.Skip(skip).Take(pageSize).ToArray()
        };
    }

    public async ValueTask<IReadOnlyCollection<AIDataSource>> GetAllAsync()
    {
        var document = await _documentManager.GetOrCreateImmutableAsync();

        return document.Records.Values.ToArray();
    }

    public async ValueTask<IReadOnlyCollection<AIDataSource>> GetAsync(IEnumerable<string> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var document = await _documentManager.GetOrCreateImmutableAsync();

        return ids.Where(document.Records.ContainsKey)
                .Select(id => document.Records[id])
                .ToArray();
    }

    public async ValueTask CreateAsync(AIDataSource record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var document = await _documentManager.GetOrCreateMutableAsync();

        if (string.IsNullOrEmpty(record.Id))
        {
            record.Id = IdGenerator.GenerateId();
        }

        document.Records[record.Id] = record;

        await _documentManager.UpdateAsync(document);
    }

    public async ValueTask UpdateAsync(AIDataSource record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var document = await _documentManager.GetOrCreateMutableAsync();

        if (string.IsNullOrEmpty(record.Id))
        {
            record.Id = IdGenerator.GenerateId();
        }

        document.Records[record.Id] = record;

        await _documentManager.UpdateAsync(document);
    }

    public ValueTask SaveChangesAsync()
    {
        return ValueTask.CompletedTask;
    }

    private async ValueTask<IReadOnlyCollection<AIDataSource>> LocateInstancesAsync(QueryContext context)
    {
        var document = await _documentManager.GetOrCreateImmutableAsync();

        if (context == null)
        {
            return document.Records.Values;
        }

        var records = GetSortable(context, document.Records.Values.AsEnumerable());

        return records.ToArray();
    }

    private static IEnumerable<AIDataSource> GetSortable(QueryContext context, IEnumerable<AIDataSource> records)
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
