using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Models;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.Tests.Core.Services;

public sealed class CatalogTests
{
    private sealed class TestCatalogEntry : CatalogEntry
    {
        public override bool Equals(object obj)
        {
            if (obj is not TestCatalogEntry other)
            {
                return false;
            }

            return string.Equals(Id, other.Id, StringComparison.Ordinal)
                && GetType() == other.GetType();
        }

        public override int GetHashCode()
        {
            return (Id?.GetHashCode() ?? 0) ^ GetType().GetHashCode();
        }
    }

    private sealed class FakeDocumentManager : IDocumentManager<DictionaryDocument<TestCatalogEntry>>
    {
        private readonly DictionaryDocument<TestCatalogEntry> _doc;

        public bool UpdateCalled { get; private set; }

        public FakeDocumentManager(Dictionary<string, TestCatalogEntry> records)
        {
            _doc = new DictionaryDocument<TestCatalogEntry> { Records = records };
        }

        public Task<DictionaryDocument<TestCatalogEntry>> GetOrCreateMutableAsync(bool reload = false)
            => Task.FromResult(_doc);

        public Task<DictionaryDocument<TestCatalogEntry>> GetOrCreateImmutableAsync(bool reload = false)
            => Task.FromResult(_doc);

        public Task<DictionaryDocument<TestCatalogEntry>> GetOrCreateMutableAsync(Func<Task<DictionaryDocument<TestCatalogEntry>>> factory)
            => Task.FromResult(_doc);

        public Task<DictionaryDocument<TestCatalogEntry>> GetOrCreateImmutableAsync(Func<Task<DictionaryDocument<TestCatalogEntry>>> factory)
            => Task.FromResult(_doc);

        public Task UpdateAsync(DictionaryDocument<TestCatalogEntry> document)
        {
            UpdateCalled = true; return Task.CompletedTask;
        }

        public Task UpdateAsync(DictionaryDocument<TestCatalogEntry> document, Func<DictionaryDocument<TestCatalogEntry>, Task> afterUpdate)
        {
            UpdateCalled = true;

            return afterUpdate != null ? afterUpdate(document) : Task.CompletedTask;
        }
    }

    private static Catalog<TestCatalogEntry> CreateCatalog(Dictionary<string, TestCatalogEntry> records, out FakeDocumentManager fakeManager)
    {
        fakeManager = new FakeDocumentManager(records);

        return new Catalog<TestCatalogEntry>(fakeManager);
    }

    [Fact]
    public async Task DeleteAsync_RemovesEntry_WhenExists()
    {
        var entry = new TestCatalogEntry { Id = "1" };
        var records = new Dictionary<string, TestCatalogEntry> { ["1"] = entry };
        var catalog = CreateCatalog(records, out var fakeManager);

        var result = await catalog.DeleteAsync(entry);

        Assert.True(result);
        Assert.False(records.ContainsKey("1"));
        Assert.True(fakeManager.UpdateCalled);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenNotExists()
    {
        var entry = new TestCatalogEntry { Id = "2" };
        var records = new Dictionary<string, TestCatalogEntry>();
        var catalog = CreateCatalog(records, out var fakeManager);

        var result = await catalog.DeleteAsync(entry);

        Assert.False(result);
        Assert.False(fakeManager.UpdateCalled);
    }

    [Fact]
    public async Task DeleteAsync_Throws_WhenNull()
    {
        var catalog = CreateCatalog(new Dictionary<string, TestCatalogEntry>(), out _);
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await catalog.DeleteAsync(null));
    }

    [Fact]
    public async Task FindByIdAsync_ReturnsEntry_WhenExists()
    {
        var entry = new TestCatalogEntry { Id = "1" };
        var records = new Dictionary<string, TestCatalogEntry> { ["1"] = entry };
        var catalog = CreateCatalog(records, out _);

        var result = await catalog.FindByIdAsync("1");

        Assert.Equal(entry, result);
    }

    [Fact]
    public async Task FindByIdAsync_ReturnsNull_WhenNotExists()
    {
        var records = new Dictionary<string, TestCatalogEntry>();
        var catalog = CreateCatalog(records, out _);

        var result = await catalog.FindByIdAsync("notfound");

        Assert.Null(result);
    }

    [Fact]
    public async Task FindByIdAsync_Throws_WhenNullOrEmpty()
    {
        var catalog = CreateCatalog([], out _);
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await catalog.FindByIdAsync(null));
        await Assert.ThrowsAsync<ArgumentException>(async () => await catalog.FindByIdAsync(""));
    }

    [Fact]
    public async Task PageAsync_ReturnsPagedResults()
    {
        var entries = Enumerable.Range(1, 10).Select(i => new TestCatalogEntry { Id = i.ToString() }).ToDictionary(e => e.Id);
        var catalog = CreateCatalog(entries, out _);
        var context = new QueryContext();

        var result = await catalog.PageAsync(2, 3, context);

        Assert.Equal(10, result.Count);
        Assert.Equal(3, result.Entries.Count());
        Assert.Equal("4", result.Entries.First().Id);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllEntries()
    {
        var entries = new Dictionary<string, TestCatalogEntry>
        {
            ["1"] = new TestCatalogEntry { Id = "1" },
            ["2"] = new TestCatalogEntry { Id = "2" }
        };
        var catalog = CreateCatalog(entries, out _);

        var result = await catalog.GetAllAsync();

        Assert.Equal(2, result.Count());
    }

    [Fact]
    public async Task CreateAsync_AddsEntry()
    {
        var records = new Dictionary<string, TestCatalogEntry>();
        var catalog = CreateCatalog(records, out var fakeManager);
        var entry = new TestCatalogEntry { Id = "new" };

        await catalog.CreateAsync(entry);

        Assert.True(records.ContainsKey("new"));
        Assert.True(fakeManager.UpdateCalled);
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenNull()
    {
        var catalog = CreateCatalog(new Dictionary<string, TestCatalogEntry>(), out _);
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await catalog.CreateAsync(null));
    }

    [Fact]
    public async Task UpdateAsync_UpdatesEntry()
    {
        var records = new Dictionary<string, TestCatalogEntry>
        {
            ["1"] = new TestCatalogEntry { Id = "1" }
        };
        var catalog = CreateCatalog(records, out var fakeManager);
        var entry = new TestCatalogEntry { Id = "1" };

        await catalog.UpdateAsync(entry);

        Assert.True(records.ContainsKey("1"));
        Assert.True(fakeManager.UpdateCalled);
    }

    [Fact]
    public async Task UpdateAsync_Throws_WhenNull()
    {
        var catalog = CreateCatalog(new Dictionary<string, TestCatalogEntry>(), out _);
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await catalog.UpdateAsync(null));
    }

    [Fact]
    public async Task SaveChangesAsync_Completes()
    {
        var catalog = CreateCatalog(new Dictionary<string, TestCatalogEntry>(), out _);
        await catalog.SaveChangesAsync();
        // No exception means pass
    }
}
