using CrestApps.Core.Models;
using CrestApps.Core.Services;

namespace CrestApps.OrchardCore.Tests.Doubles;

/// <summary>
/// A catalog store kept in memory, in the order records were created. Derive from it to add the lookups of a store
/// interface.
/// </summary>
/// <typeparam name="T">The record type.</typeparam>
public class InMemoryCatalog<T> : ICatalog<T>
    where T : CatalogItem
{
    /// <summary>
    /// Gets the stored records.
    /// </summary>
    public List<T> Entries { get; } = [];

    /// <summary>
    /// Gets how many times a record was updated.
    /// </summary>
    public int UpdateCount { get; private set; }

    public ValueTask<T> FindByIdAsync(string id, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(Entries.FirstOrDefault(entry => entry.ItemId == id));

    public ValueTask<IReadOnlyCollection<T>> GetAllAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult<IReadOnlyCollection<T>>(Entries.ToList());

    public ValueTask<IReadOnlyCollection<T>> GetAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        var wanted = new HashSet<string>(ids, StringComparer.Ordinal);

        return ValueTask.FromResult<IReadOnlyCollection<T>>(Entries.Where(entry => wanted.Contains(entry.ItemId)).ToList());
    }

    public ValueTask<PageResult<T>> PageAsync<TQuery>(int page, int pageSize, TQuery context, CancellationToken cancellationToken = default)
        where TQuery : QueryContext
        => throw new NotSupportedException();

    public ValueTask<bool> DeleteAsync(T entry, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(Entries.Remove(entry));

    public ValueTask CreateAsync(T entry, CancellationToken cancellationToken = default)
    {
        Entries.Add(entry);

        return ValueTask.CompletedTask;
    }

    public ValueTask UpdateAsync(T entry, CancellationToken cancellationToken = default)
    {
        UpdateCount++;

        return ValueTask.CompletedTask;
    }
}
