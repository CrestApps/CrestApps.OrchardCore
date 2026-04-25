using CrestApps.Core.AI.Models;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.Models;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.Tests.Modules.AI.DataSources;

public sealed class DefaultAIDataSourceStoreTests
{
    [Fact]
    public async Task PageAsync_FiltersByDisplayTextContainingSearchTerm()
    {
        var store = new DefaultAIDataSourceStore(new FakeDocumentManager<AIDataSource>([
            new AIDataSource
            {
                ItemId = "1",
                DisplayText = "Alpha source",
            },
            new AIDataSource
            {
                ItemId = "2",
                DisplayText = "Bravo source",
            },
        ]));
        var context = new QueryContext
        {
            Name = "Alpha",
        };

        var result = await store.PageAsync(1, 10, context, TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Count);
        Assert.Single(result.Entries);
        Assert.Equal("Alpha source", result.Entries.First().DisplayText);
    }

    private sealed class FakeDocumentManager<T> : IDocumentManager<DictionaryDocument<T>>
        where T : CatalogItem
    {
        private readonly DictionaryDocument<T> _document;

        public FakeDocumentManager(IEnumerable<T> records)
        {
            _document = new DictionaryDocument<T>
            {
                Records = records.ToDictionary(x => x.ItemId),
            };
        }

        public Task<DictionaryDocument<T>> GetOrCreateMutableAsync() => Task.FromResult(_document);

        public Task<DictionaryDocument<T>> GetOrCreateImmutableAsync() => Task.FromResult(_document);

        public Task<DictionaryDocument<T>> GetOrCreateMutableAsync(Func<Task<DictionaryDocument<T>>> factory) => Task.FromResult(_document);

        public Task<DictionaryDocument<T>> GetOrCreateImmutableAsync(Func<Task<DictionaryDocument<T>>> factory) => Task.FromResult(_document);

        public Task UpdateAsync(DictionaryDocument<T> document) => _document == document ? Task.CompletedTask : Task.CompletedTask;

        public Task UpdateAsync(DictionaryDocument<T> document, Func<DictionaryDocument<T>, Task> afterUpdate)
            => afterUpdate == null ? Task.CompletedTask : afterUpdate(document);
    }
}
