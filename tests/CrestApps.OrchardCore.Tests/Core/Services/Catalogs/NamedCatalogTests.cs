using CrestApps.OrchardCore.Tests.Core.Services.Catalogs.Services;

namespace CrestApps.OrchardCore.Tests.Core.Services.Catalogs;

public sealed class NamedCatalogTests
{
    [Fact]
    public async Task FindByNameAsync_ReturnsEntry_WhenExists()
    {
        var entry = new TestNamedCatalogEntry { Id = "1", Name = "Test" };
        var records = new Dictionary<string, TestNamedCatalogEntry> { ["1"] = entry };
        var catalog = FakeDocumentManager.CreateNamedCatalog(records, out _);

        var result = await catalog.FindByNameAsync("Test");

        Assert.Equal(entry, result);
    }

    [Fact]
    public async Task FindByNameAsync_ReturnsNull_WhenNotExists()
    {
        var records = new Dictionary<string, TestNamedCatalogEntry>();
        var catalog = FakeDocumentManager.CreateNamedCatalog(records, out _);

        var result = await catalog.FindByNameAsync("NotFound");

        Assert.Null(result);
    }

    [Fact]
    public async Task FindByNameAsync_Throws_WhenNullOrEmpty()
    {
        var catalog = FakeDocumentManager.CreateNamedCatalog<TestNamedCatalogEntry>([], out _);
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await catalog.FindByNameAsync(null));
        await Assert.ThrowsAsync<ArgumentException>(async () => await catalog.FindByNameAsync(""));
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenDuplicateName()
    {
        var entry1 = new TestNamedCatalogEntry { Id = "1", Name = "Test" };
        var entry2 = new TestNamedCatalogEntry { Id = "2", Name = "Test" };
        var records = new Dictionary<string, TestNamedCatalogEntry> { ["1"] = entry1 };
        var catalog = FakeDocumentManager.CreateNamedCatalog(records, out var fakeManager);

        await catalog.CreateAsync(entry1);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await catalog.CreateAsync(entry2));
    }

    [Fact]
    public async Task UpdateAsync_Throws_WhenDuplicateName()
    {
        var entry1 = new TestNamedCatalogEntry { Id = "1", Name = "Test1" };
        var entry2 = new TestNamedCatalogEntry { Id = "2", Name = "Test2" };

        var records = new Dictionary<string, TestNamedCatalogEntry>
        {
            ["1"] = entry1,
            ["2"] = entry2,
        };
        var catalog = FakeDocumentManager.CreateNamedCatalog(records, out var fakeManager);
        entry1.Name = entry2.Name;

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await catalog.UpdateAsync(entry1));
    }
}
