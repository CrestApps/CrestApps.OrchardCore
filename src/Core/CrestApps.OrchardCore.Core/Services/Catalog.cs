using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Models;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.Core.Services;

/// <summary>
/// Document-backed implementation of <see cref="ICatalog{T}"/> that stores catalog entries
/// in an OrchardCore <see cref="DictionaryDocument{T}"/>.
/// </summary>
/// <typeparam name="T">The type of catalog item managed by this catalog.</typeparam>
public class Catalog<T> : ICatalog<T>
    where T : CatalogItem
{
    /// <summary>
    /// The document manager used to read and write the backing document.
    /// </summary>
    protected readonly IDocumentManager<DictionaryDocument<T>> DocumentManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="Catalog{T}"/> class.
    /// </summary>
    /// <param name="documentManager">The document manager for accessing the backing document.</param>
    public Catalog(IDocumentManager<DictionaryDocument<T>> documentManager)
    {
        DocumentManager = documentManager;
    }

    /// <inheritdoc />
    public async ValueTask<bool> DeleteAsync(T entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var document = await DocumentManager.GetOrCreateMutableAsync();

        if (!document.Records.TryGetValue(entry.ItemId, out var existingInstance))
        {
            return false;
        }

        Deleting(entry, document);

        var removed = document.Records.Remove(entry.ItemId);

        if (removed)
        {
            await DocumentManager.UpdateAsync(document);
        }

        return removed;
    }

    /// <inheritdoc />
    public async ValueTask<T> FindByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        var document = await DocumentManager.GetOrCreateImmutableAsync();

        if (document.Records.TryGetValue(id, out var record))
        {
            return Clone(record);
        }

        return null;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyCollection<T>> GetAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var document = await DocumentManager.GetOrCreateImmutableAsync();

        return ids.Where(document.Records.ContainsKey)
            .Select(id => Clone(document.Records[id]))
            .ToArray();
    }

    /// <inheritdoc />
    public async ValueTask<PageResult<T>> PageAsync<TQuery>(
        int page,
        int pageSize,
        TQuery context,
        CancellationToken cancellationToken = default)
        where TQuery : QueryContext
    {
        var records = await LocateInstancesAsync(context, cancellationToken);

        var skip = (page - 1) * pageSize;

        return new PageResult<T>
        {
            Count = records.Count(),
            Entries = records.Skip(skip).Take(pageSize).Select(Clone).ToArray()
        };
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyCollection<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var document = await DocumentManager.GetOrCreateImmutableAsync();

        return document.Records.Values.Select(Clone).ToArray();
    }

    /// <inheritdoc />
    public async ValueTask CreateAsync(T record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        var document = await DocumentManager.GetOrCreateMutableAsync();

        if (string.IsNullOrEmpty(record.ItemId))
        {
            record.ItemId = UniqueId.GenerateId();
        }
        else if (document.Records.ContainsKey(record.ItemId))
        {
            throw new InvalidOperationException($"A record with the ItemId '{record.ItemId}' already exists. Use {nameof(UpdateAsync)} to modify existing records.");
        }

        Saving(record, document);

        document.Records[record.ItemId] = record;

        await DocumentManager.UpdateAsync(document);
    }

    /// <inheritdoc />
    public async ValueTask UpdateAsync(T record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        var document = await DocumentManager.GetOrCreateMutableAsync();

        if (string.IsNullOrEmpty(record.ItemId) || !document.Records.ContainsKey(record.ItemId))
        {
            throw new InvalidOperationException($"Cannot update a record that does not exist. Use {nameof(CreateAsync)} to create new records.");
        }

        Saving(record, document);

        document.Records[record.ItemId] = record;

        await DocumentManager.UpdateAsync(document);
    }

    protected virtual void Deleting(T model, DictionaryDocument<T> document)
    {
    }

    protected virtual async ValueTask<IEnumerable<T>> LocateInstancesAsync(
        QueryContext context,
        CancellationToken cancellationToken = default)
    {
        var document = await DocumentManager.GetOrCreateImmutableAsync();

        if (context == null)
        {
            return document.Records.Values;
        }

        var records = GetSortable(context, document.Records.Values.AsEnumerable());

        return records;
    }

    protected virtual IEnumerable<T> GetSortable(QueryContext context, IEnumerable<T> records)
    {
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

    protected virtual void Saving(T record, DictionaryDocument<T> document)
    {
    }

    protected static T Clone(T record)
    {
        if (record is ICloneable<T> cloneableOfT)
        {
            return cloneableOfT.Clone();
        }

        if (record is ICloneable clonable)
        {
            return (T)clonable.Clone();
        }

        return record;
    }
}
