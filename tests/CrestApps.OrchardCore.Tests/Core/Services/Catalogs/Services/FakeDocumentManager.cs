using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Models;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.Tests.Core.Services.Catalogs.Services;

internal sealed class FakeDocumentManager<T> : IDocumentManager<DictionaryDocument<T>>
{
    private readonly DictionaryDocument<T> _doc;

    public bool UpdateCalled { get; private set; }

    public FakeDocumentManager(Dictionary<string, T> records)
    {
        _doc = new DictionaryDocument<T> { Records = records };
    }

    public Task<DictionaryDocument<T>> GetOrCreateMutableAsync(bool reload = false)
        => Task.FromResult(_doc);

    public Task<DictionaryDocument<T>> GetOrCreateImmutableAsync(bool reload = false)
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
    internal static Catalog<TCatalog> CreateCatalog<TCatalog>(Dictionary<string, TCatalog> records, out FakeDocumentManager<TCatalog> fakeManager)
        where TCatalog : CatalogEntry
    {
        fakeManager = new FakeDocumentManager<TCatalog>(records);

        return new Catalog<TCatalog>(fakeManager);
    }

    internal static NamedCatalog<TCatalog> CreateNamedCatalog<TCatalog>(Dictionary<string, TCatalog> records, out FakeDocumentManager<TCatalog> fakeManager)
        where TCatalog : CatalogEntry, INameAwareModel
    {
        fakeManager = new FakeDocumentManager<TCatalog>(records);

        return new NamedCatalog<TCatalog>(fakeManager);
    }
}
