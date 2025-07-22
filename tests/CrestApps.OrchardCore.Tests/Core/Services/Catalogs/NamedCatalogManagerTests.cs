using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Services;
using CrestApps.OrchardCore.Tests.Core.Services.Catalogs.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace CrestApps.OrchardCore.Tests.Core.Services.Catalogs;

public sealed class NamedCatalogManagerTests
{
    private static NamedCatalogManager<TestNamedCatalogEntry> CreateManager(List<TestNamedCatalogEntry> records = null)
    {
        records ??= new List<TestNamedCatalogEntry>();
        var catalog = FakeDocumentManager.CreateNamedCatalog(records, out _);
        var logger = Mock.Of<ILogger<CatalogManager<TestNamedCatalogEntry>>>();
        return new NamedCatalogManager<TestNamedCatalogEntry>(catalog, System.Linq.Enumerable.Empty<ICatalogEntryHandler<TestNamedCatalogEntry>>(), logger);
    }

    [Fact]
    public async Task FindByNameAsync_ReturnsEntry_WhenExists()
    {
        var entry = new TestNamedCatalogEntry { Id = "1", Name = "Test" };
        var manager = CreateManager(new List<TestNamedCatalogEntry> { entry });
        var result = await manager.FindByNameAsync("Test");
        Assert.Equal(entry, result);
    }

    [Fact]
    public async Task CreateAsync_AddsEntry()
    {
        var records = new List<TestNamedCatalogEntry>();
        var manager = CreateManager(records);
        var entry = new TestNamedCatalogEntry { Id = "new", Name = "Test" };
        await manager.CreateAsync(entry);
        var result = await manager.FindByNameAsync("Test");
        Assert.NotNull(result);
        Assert.Equal(entry, result);
    }

    [Fact]
    public async Task FindByNameAsync_InvokesLoadedHandler()
    {
        var entry = new TestNamedCatalogEntry { Id = "1", Name = "Test" };
        var records = new List<TestNamedCatalogEntry> { entry };
        var catalog = FakeDocumentManager.CreateNamedCatalog(records, out _);
        var logger = Mock.Of<ILogger<CatalogManager<TestNamedCatalogEntry>>>();
        var callOrder = new Queue<string>();
        var existsInCatalogDuringLoaded = false;

        var handler = new TestCatalogEntryHandler<TestNamedCatalogEntry>
        {
            OnLoadedAsync = async ctx =>
            {
                existsInCatalogDuringLoaded = await catalog.FindByNameAsync(entry.Name) != null;
                callOrder.Enqueue("LoadedAsync");
            }
        };

        var manager = new NamedCatalogManager<TestNamedCatalogEntry>(catalog, [handler], logger);
        await manager.FindByNameAsync("Test");

        Assert.Equal("LoadedAsync", callOrder.Dequeue());
        Assert.Empty(callOrder);
        Assert.True(existsInCatalogDuringLoaded);
    }
}
