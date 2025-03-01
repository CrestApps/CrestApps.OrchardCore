using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Services;
using OrchardCore;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.Core.Services;

public class ModelStore<T> : IModelStore<T>
    where T : SourceModel
{
    protected readonly IDocumentManager<ModelDocument<T>> DocumentManager;

    public ModelStore(IDocumentManager<ModelDocument<T>> documentManager)
    {
        DocumentManager = documentManager;
    }

    public async ValueTask<bool> DeleteAsync(T model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var document = await DocumentManager.GetOrCreateMutableAsync();

        if (!document.Records.TryGetValue(model.Id, out var existingInstance))
        {
            return false;
        }

        Deleting(model, document);

        var removed = document.Records.Remove(model.Id);

        if (removed)
        {
            await DocumentManager.UpdateAsync(document);
        }

        return removed;
    }

    protected virtual void Deleting(T model, ModelDocument<T> document)
    {
    }

    public async ValueTask<T> FindByIdAsync(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        var document = await DocumentManager.GetOrCreateImmutableAsync();

        if (document.Records.TryGetValue(id, out var record))
        {
            return record;
        }

        return null;
    }

    public async ValueTask SaveAsync(T record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var document = await DocumentManager.GetOrCreateMutableAsync();

        if (string.IsNullOrEmpty(record.Id))
        {
            record.Id = IdGenerator.GenerateId();
        }

        Saving(record, document);

        document.Records[record.Id] = record;

        await DocumentManager.UpdateAsync(document);
    }

    public async ValueTask<PageResult<T>> PageAsync<TQuery>(int page, int pageSize, TQuery context)
            where TQuery : QueryContext
    {
        var records = await LocateInstancesAsync(context);

        var skip = (page - 1) * pageSize;

        return new PageResult<T>
        {
            Count = records.Count(),
            Models = records.Skip(skip).Take(pageSize).ToArray()
        };
    }

    public async ValueTask<IEnumerable<T>> GetAllAsync()
    {
        var document = await DocumentManager.GetOrCreateImmutableAsync();

        return document.Records.Values;
    }

    public async ValueTask<IEnumerable<T>> GetAsync(string source)
    {
        var document = await DocumentManager.GetOrCreateImmutableAsync();

        return document.Records.Values.Where(x => x.Source.Equals(source, StringComparison.OrdinalIgnoreCase));
    }

    protected virtual async ValueTask<IEnumerable<T>> LocateInstancesAsync(QueryContext context)
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

    protected virtual IEnumerable<T> GetSortable(QueryContext context, IEnumerable<T> records)
    {
        if (!string.IsNullOrEmpty(context.Source))
        {
            records = records.Where(x => x.Source.Equals(context.Source, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(context.Name))
        {
            records = records.Where(x => (x is INameAwareModel named && named.Name.Contains(context.Name, StringComparison.OrdinalIgnoreCase)) ||
             (x is IDisplayTextAwareModel displayModel && displayModel.DisplayText.Contains(context.Name, StringComparison.OrdinalIgnoreCase)) ||
             (x is not INameAwareModel && x is not IDisplayTextAwareModel));
        }

        if (context.Sorted)
        {
            records = records.OrderBy(x => x is IDisplayTextAwareModel displayModel ? displayModel.DisplayText : x is INameAwareModel named ? named.Name : string.Empty);
        }

        return records;
    }

    protected virtual void Saving(T record, ModelDocument<T> document)
    {
    }
}
