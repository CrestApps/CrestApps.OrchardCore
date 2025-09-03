using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Models;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.Tests.Core.Services.Catalogs.Services;

internal sealed class FakeDocumentManager<T> : IDocumentManager<DictionaryDocument<T>>
    where T : CatalogEntry
{
    private readonly DictionaryDocument<T> _doc;

    public bool UpdateCalled { get; private set; }

    public FakeDocumentManager(IEnumerable<T> records)
    {
        _doc = new DictionaryDocument<T>
        {
            Records = records.ToDictionary(x => x.Id),
        };
    }

    public Task<DictionaryDocument<T>> GetOrCreateMutableAsync()
        => Task.FromResult(_doc);

    public Task<DictionaryDocument<T>> GetOrCreateImmutableAsync()
        => Task.FromResult(_doc);

    public Task<DictionaryDocument<T>> GetOrCreateMutableAsync(Func<Task<DictionaryDocument<T>>> factory)
        => Task.FromResult(_doc);

    public Task<DictionaryDocument<T>> GetOrCreateImmutableAsync(Func<Task<DictionaryDocument<T>>> factory)
        => Task.FromResult(_doc);

    public Task UpdateAsync(DictionaryDocument<T> document)
    {
        UpdateCalled = true;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(DictionaryDocument<T> document, Func<DictionaryDocument<T>, Task> afterUpdate)
    {
        UpdateCalled = true;
        return afterUpdate != null ? afterUpdate(document) : Task.CompletedTask;
    }
}

internal sealed class FakeDocumentManager
{
    internal static Catalog<TCatalog> CreateCatalog<TCatalog>(IEnumerable<TCatalog> records, out FakeDocumentManager<TCatalog> fakeManager)
        where TCatalog : CatalogEntry
    {
        fakeManager = new FakeDocumentManager<TCatalog>(records);

        return new Catalog<TCatalog>(fakeManager);
    }

    internal static NamedCatalog<TCatalog> CreateNamedCatalog<TCatalog>(IEnumerable<TCatalog> records, out FakeDocumentManager<TCatalog> fakeManager)
        where TCatalog : CatalogEntry, INameAwareModel
    {
        fakeManager = new FakeDocumentManager<TCatalog>(records);

        return new NamedCatalog<TCatalog>(fakeManager);
    }

    internal static NamedSourceCatalog<TCatalog> CreateNamedSourceCatalog<TCatalog>(IEnumerable<TCatalog> records, out FakeDocumentManager<TCatalog> fakeManager)
        where TCatalog : CatalogEntry, INameAwareModel, ISourceAwareModel
    {
        fakeManager = new FakeDocumentManager<TCatalog>(records);

        return new NamedSourceCatalog<TCatalog>(fakeManager);
    }
}
