using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;

namespace CrestApps.OrchardCore.Tests.Doubles;

/// <summary>
/// A named catalog kept in memory: an <see cref="InMemoryCatalog{T}"/> that also finds a record by its name, ignoring
/// case.
/// </summary>
/// <typeparam name="T">The record type.</typeparam>
public class InMemoryNamedCatalog<T> : InMemoryCatalog<T>, INamedCatalog<T>
    where T : CatalogItem, INameAwareModel
{
    public ValueTask<T> FindByNameAsync(string name, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(Entries.FirstOrDefault(entry => string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase)));
}
