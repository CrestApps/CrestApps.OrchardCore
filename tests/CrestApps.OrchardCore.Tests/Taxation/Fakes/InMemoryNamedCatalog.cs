using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Models;

namespace CrestApps.OrchardCore.Tests.Taxation.Fakes;

/// <summary>
/// A minimal in-memory <see cref="INamedCatalog{T}"/> used by the taxation tests to back the real
/// catalog providers and resolvers without a document store or dependency injection.
/// </summary>
public class InMemoryNamedCatalog<T> : INamedCatalog<T>
    where T : CatalogItem, INameAwareModel
{
    private readonly Dictionary<string, T> _records = new(StringComparer.Ordinal);

    public ValueTask CreateAsync(T entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (string.IsNullOrEmpty(entry.ItemId))
        {
            entry.ItemId = UniqueId.GenerateId();
        }

        _records[entry.ItemId] = Clone(entry);

        return ValueTask.CompletedTask;
    }

    public ValueTask UpdateAsync(T entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        _records[entry.ItemId] = Clone(entry);

        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> DeleteAsync(T entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return ValueTask.FromResult(_records.Remove(entry.ItemId));
    }

    public ValueTask<T> FindByIdAsync(string id, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_records.TryGetValue(id, out var record) ? Clone(record) : null);

    public ValueTask<T> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var record = _records.Values.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));

        return ValueTask.FromResult(record is null ? null : Clone(record));
    }

    public ValueTask<IReadOnlyCollection<T>> GetAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        var result = ids.Where(_records.ContainsKey).Select(id => Clone(_records[id])).ToArray();

        return ValueTask.FromResult<IReadOnlyCollection<T>>(result);
    }

    public ValueTask<IReadOnlyCollection<T>> GetAllAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult<IReadOnlyCollection<T>>(_records.Values.Select(Clone).ToArray());

    public ValueTask<PageResult<T>> PageAsync<TQuery>(int page, int pageSize, TQuery context, CancellationToken cancellationToken = default)
        where TQuery : QueryContext
    {
        var records = _records.Values.Select(Clone).ToArray();
        var skip = (page - 1) * pageSize;

        var result = new PageResult<T>
        {
            Count = records.Length,
            Entries = records.Skip(skip).Take(pageSize).ToArray(),
        };

        return ValueTask.FromResult(result);
    }

    private static T Clone(T record)
        => record is ICloneable<T> cloneable ? cloneable.Clone() : record;
}
