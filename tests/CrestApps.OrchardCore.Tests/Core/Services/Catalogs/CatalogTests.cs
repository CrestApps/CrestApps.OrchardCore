using CrestApps.Core.Models;
using CrestApps.OrchardCore.Tests.Core.Services.Catalogs.Services;

namespace CrestApps.OrchardCore.Tests.Core.Services.Catalogs;

public sealed partial class CatalogTests
{
    [Fact]
    public async Task DeleteAsync_RemovesEntry_WhenExists()
    {
        var entry = new TestCatalogEntry { ItemId = "1" };
        var catalog = FakeDocumentManager.CreateCatalog([entry], out var fakeManager);

        var result = await catalog.DeleteAsync(entry, TestContext.Current.CancellationToken);

        Assert.True(result);
        var resultEntry = await catalog.FindByIdAsync("1", TestContext.Current.CancellationToken);
        Assert.Null(resultEntry);
        Assert.True(fakeManager.UpdateCalled);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenNotExists()
    {
        var entry = new TestCatalogEntry { ItemId = "2" };
        var catalog = FakeDocumentManager.CreateCatalog<TestCatalogEntry>([], out var fakeManager);

        var result = await catalog.DeleteAsync(entry, TestContext.Current.CancellationToken);

        Assert.False(result);
        Assert.False(fakeManager.UpdateCalled);
    }

    [Fact]
    public async Task DeleteAsync_Throws_WhenNull()
    {
        var catalog = FakeDocumentManager.CreateCatalog<TestCatalogEntry>([], out _);
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await catalog.DeleteAsync(null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task FindByIdAsync_ReturnsEntry_WhenExists()
    {
        var entry = new TestCatalogEntry { ItemId = "1" };
        var catalog = FakeDocumentManager.CreateCatalog([entry], out _);

        var result = await catalog.FindByIdAsync("1", TestContext.Current.CancellationToken);

        Assert.Equal(entry, result);
    }

    [Fact]
    public async Task FindByIdAsync_ReturnsNull_WhenNotExists()
    {
        var catalog = FakeDocumentManager.CreateCatalog<TestCatalogEntry>([], out _);

        var result = await catalog.FindByIdAsync("notfound", TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task FindByIdAsync_Throws_WhenNullOrEmpty()
    {
        var catalog = FakeDocumentManager.CreateCatalog<TestCatalogEntry>([], out _);
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await catalog.FindByIdAsync(null, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(async () => await catalog.FindByIdAsync("", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PageAsync_ReturnsPagedResults()
    {
        var entries = Enumerable.Range(1, 10)
            .Select(i => new TestCatalogEntry { ItemId = i.ToString() });

        var catalog = FakeDocumentManager.CreateCatalog(entries, out _);
        var context = new QueryContext();

        var result = await catalog.PageAsync(2, 3, context, TestContext.Current.CancellationToken);

        Assert.Equal(10, result.Count);
        Assert.Equal(3, result.Entries.Count);
        Assert.Equal("4", result.Entries.First().ItemId);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllEntries()
    {
        var entries = new List<TestCatalogEntry>
        {
            new() { ItemId = "1" },
            new() { ItemId = "2" }
        };

        var catalog = FakeDocumentManager.CreateCatalog(entries, out _);

        var result = await catalog.GetAllAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task CreateAsync_AddsEntry()
    {
        var records = new List<TestCatalogEntry>();
        var catalog = FakeDocumentManager.CreateCatalog(records, out var fakeManager);
        var entry = new TestCatalogEntry { ItemId = "new" };

        await catalog.CreateAsync(entry, TestContext.Current.CancellationToken);

        var result = await catalog.FindByIdAsync("new", TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.True(fakeManager.UpdateCalled);
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenNull()
    {
        var catalog = FakeDocumentManager.CreateCatalog(new List<TestCatalogEntry>(), out _);
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await catalog.CreateAsync(null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateAsync_UpdatesEntry()
    {
        var records = new List<TestCatalogEntry>
        {
            new() { ItemId = "1" }
        };
        var catalog = FakeDocumentManager.CreateCatalog(records, out var fakeManager);
        var entry = new TestCatalogEntry { ItemId = "1" };

        await catalog.UpdateAsync(entry, TestContext.Current.CancellationToken);

        Assert.Contains(records, r => r.ItemId == "1");
        Assert.True(fakeManager.UpdateCalled);
    }

    [Fact]
    public async Task UpdateAsync_Throws_WhenNull()
    {
        var catalog = FakeDocumentManager.CreateCatalog(new List<TestCatalogEntry>(), out _);
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await catalog.UpdateAsync(null, TestContext.Current.CancellationToken));
    }
}
