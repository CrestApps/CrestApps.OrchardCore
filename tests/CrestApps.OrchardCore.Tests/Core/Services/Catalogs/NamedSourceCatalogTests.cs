using CrestApps.OrchardCore.Tests.Core.Services.Catalogs.Services;

namespace CrestApps.OrchardCore.Tests.Core.Services.Catalogs;

public sealed class NamedSourceCatalogTests
{
    [Fact]
    public async Task FindByNameAsync_ReturnsEntry_WhenExists()
    {
        var entry = new TestNamedSourceCatalogEntry { Id = "1", Name = "Test", Source = "A" };
        var records = new Dictionary<string, TestNamedSourceCatalogEntry> { ["1"] = entry };
        var catalog = FakeDocumentManager.CreateNamedSourceCatalog(records, out _);

        var result = await catalog.FindByNameAsync("Test");

        Assert.Equal(entry, result);
    }

    [Fact]
    public async Task FindByNameAsync_ReturnsNull_WhenNotExists()
    {
        var records = new Dictionary<string, TestNamedSourceCatalogEntry>();
        var catalog = FakeDocumentManager.CreateNamedSourceCatalog(records, out _);

        var result = await catalog.FindByNameAsync("NotFound");

        Assert.Null(result);
    }

    [Fact]
    public async Task FindByNameAsync_Throws_WhenNullOrEmpty()
    {
        var catalog = FakeDocumentManager.CreateNamedSourceCatalog(new Dictionary<string, TestNamedSourceCatalogEntry>(), out _);
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await catalog.FindByNameAsync(null));
        await Assert.ThrowsAsync<ArgumentException>(async () => await catalog.FindByNameAsync(""));
    }

    [Fact]
    public async Task GetAsync_ByNameAndSource_ReturnsEntry_WhenExists()
    {
        var entry = new TestNamedSourceCatalogEntry { Id = "1", Name = "Test", Source = "A" };
        var records = new Dictionary<string, TestNamedSourceCatalogEntry> { ["1"] = entry };
        var catalog = FakeDocumentManager.CreateNamedSourceCatalog(records, out _);

        var result = await catalog.GetAsync("Test", "A");

        Assert.Equal(entry, result);
    }

    [Fact]
    public async Task GetAsync_ByNameAndSource_ReturnsNull_WhenNotExists()
    {
        var entry = new TestNamedSourceCatalogEntry { Id = "1", Name = "Test", Source = "A" };
        var records = new Dictionary<string, TestNamedSourceCatalogEntry> { ["1"] = entry };
        var catalog = FakeDocumentManager.CreateNamedSourceCatalog(records, out _);

        var result = await catalog.GetAsync("Test", "B");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsync_ByNameAndSource_Throws_WhenNullOrEmpty()
    {
        var catalog = FakeDocumentManager.CreateNamedSourceCatalog(new Dictionary<string, TestNamedSourceCatalogEntry>(), out _);
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await catalog.GetAsync(null, "A"));
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await catalog.GetAsync("Test", null));
        await Assert.ThrowsAsync<ArgumentException>(async () => await catalog.GetAsync("", "A"));
        await Assert.ThrowsAsync<ArgumentException>(async () => await catalog.GetAsync("Test", ""));
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenDuplicateName()
    {
        var entry1 = new TestNamedSourceCatalogEntry { Id = "1", Name = "Test", Source = "A" };
        var entry2 = new TestNamedSourceCatalogEntry { Id = "2", Name = "Test", Source = "B" };
        var records = new Dictionary<string, TestNamedSourceCatalogEntry> { ["1"] = entry1 };
        var catalog = FakeDocumentManager.CreateNamedSourceCatalog(records, out var fakeManager);

        await catalog.CreateAsync(entry1);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await catalog.CreateAsync(entry2));
    }

    [Fact]
    public async Task UpdateAsync_Throws_WhenDuplicateName()
    {
        var entry1 = new TestNamedSourceCatalogEntry { Id = "1", Name = "Test1", Source = "A" };
        var entry2 = new TestNamedSourceCatalogEntry { Id = "2", Name = "Test2", Source = "B" };

        var records = new Dictionary<string, TestNamedSourceCatalogEntry>
        {
            ["1"] = entry1,
            ["2"] = entry2,
        };
        var catalog = FakeDocumentManager.CreateNamedSourceCatalog(records, out var fakeManager);
        entry1.Name = entry2.Name;

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await catalog.UpdateAsync(entry1));
    }
}
