using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Services;
using CrestApps.OrchardCore.Tests.Core.Services.Catalogs.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace CrestApps.OrchardCore.Tests.Core.Services.Catalogs;

public sealed class CatalogManagerTests
{
    private static CatalogManager<TestCatalogEntry> CreateManager(List<TestCatalogEntry> records = null)
    {
        records ??= [];
        var catalog = FakeDocumentManager.CreateCatalog(records, out _);
        var logger = Mock.Of<ILogger<CatalogManager<TestCatalogEntry>>>();
        return new CatalogManager<TestCatalogEntry>(catalog, [], logger);
    }

    private static (CatalogManager<TestCatalogEntry> manager, Mock<ICatalogEntryHandler<TestCatalogEntry>> handlerMock) CreateManagerWithHandler(List<TestCatalogEntry> records = null)
    {
        records ??= new List<TestCatalogEntry>();
        var catalog = FakeDocumentManager.CreateCatalog(records, out _);
        var logger = Mock.Of<ILogger<CatalogManager<TestCatalogEntry>>>();
        var handlerMock = new Mock<ICatalogEntryHandler<TestCatalogEntry>>();
        var manager = new CatalogManager<TestCatalogEntry>(catalog, [handlerMock.Object], logger);
        return (manager, handlerMock);
    }

    [Fact]
    public async Task FindByIdAsync_ReturnsEntry_WhenExists()
    {
        var entry = new TestCatalogEntry { ItemId = "1" };
        var manager = CreateManager([entry]);
        var result = await manager.FindByIdAsync("1");
        Assert.Equal(entry, result);
    }

    [Fact]
    public async Task FindByIdAsync_ReturnsNull_WhenNotExists()
    {
        var manager = CreateManager();
        var result = await manager.FindByIdAsync("notfound");
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateAsync_AddsEntry()
    {
        var records = new List<TestCatalogEntry>();
        var manager = CreateManager(records);
        var entry = new TestCatalogEntry { ItemId = "new" };
        await manager.CreateAsync(entry);
        var result = await manager.FindByIdAsync("new");
        Assert.NotNull(result);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesEntry()
    {
        var entry = new TestCatalogEntry { ItemId = "1" };
        var manager = CreateManager([entry]);
        await manager.UpdateAsync(entry);
        var result = await manager.FindByIdAsync("1");
        Assert.Equal(entry, result);
    }

    [Fact]
    public async Task DeleteAsync_RemovesEntry_WhenExists()
    {
        var entry = new TestCatalogEntry { ItemId = "1" };
        var manager = CreateManager([entry]);
        var result = await manager.DeleteAsync(entry);
        Assert.True(result);
        var deleted = await manager.FindByIdAsync("1");
        Assert.Null(deleted);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenNotExists()
    {
        var entry = new TestCatalogEntry { ItemId = "2" };
        var manager = CreateManager();
        var result = await manager.DeleteAsync(entry);
        Assert.False(result);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllEntries()
    {
        var entries = new List<TestCatalogEntry> { new() { ItemId = "1" }, new() { ItemId = "2" } };
        var manager = CreateManager(entries);
        var result = await manager.GetAllAsync();
        Assert.Equal(2, result.Count());
    }

    [Fact]
    public async Task PageAsync_ReturnsPagedResults()
    {
        var entries = Enumerable.Range(1, 10).Select(i => new TestCatalogEntry { ItemId = i.ToString() }).ToList();
        var manager = CreateManager(entries);
        var context = new QueryContext();
        var result = await manager.PageAsync(2, 3, context);
        Assert.Equal(10, result.Count);
        Assert.Equal(3, result.Entries.Count);
        Assert.Equal("4", result.Entries.First().ItemId);
    }

    [Fact]
    public async Task NewAsync_CreatesNewEntry()
    {
        var manager = CreateManager();
        var entry = await manager.NewAsync();
        Assert.NotNull(entry);
        Assert.False(string.IsNullOrEmpty(entry.ItemId));
    }

    [Fact]
    public async Task ValidateAsync_ReturnsValidationResult()
    {
        var manager = CreateManager();
        var entry = new TestCatalogEntry { ItemId = "1" };
        var result = await manager.ValidateAsync(entry);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task DeleteAsync_InvokesHandlersInOrder()
    {
        var entry = new TestCatalogEntry { ItemId = "1" };
        var (manager, handlerMock) = CreateManagerWithHandler([entry]);
        await manager.DeleteAsync(entry);
        handlerMock.Verify(h => h.DeletingAsync(It.Is<DeletingContext<TestCatalogEntry>>(ctx => ctx.Model == entry)), Times.Once);
        handlerMock.Verify(h => h.DeletedAsync(It.Is<DeletedContext<TestCatalogEntry>>(ctx => ctx.Model == entry)), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_InvokesHandlersInOrder()
    {
        var entry = new TestCatalogEntry { ItemId = "new" };
        var (manager, handlerMock) = CreateManagerWithHandler();
        await manager.CreateAsync(entry);
        handlerMock.Verify(h => h.CreatingAsync(It.Is<CreatingContext<TestCatalogEntry>>(ctx => ctx.Model == entry)), Times.Once);
        handlerMock.Verify(h => h.CreatedAsync(It.Is<CreatedContext<TestCatalogEntry>>(ctx => ctx.Model == entry)), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_InvokesHandlersInOrder()
    {
        var entry = new TestCatalogEntry { ItemId = "1" };
        var (manager, handlerMock) = CreateManagerWithHandler(new List<TestCatalogEntry> { entry });
        await manager.UpdateAsync(entry);
        handlerMock.Verify(h => h.UpdatingAsync(It.Is<UpdatingContext<TestCatalogEntry>>(ctx => ctx.Model == entry)), Times.Once);
        handlerMock.Verify(h => h.UpdatedAsync(It.Is<UpdatedContext<TestCatalogEntry>>(ctx => ctx.Model == entry)), Times.Once);
    }

    [Fact]
    public async Task ValidateAsync_InvokesHandlersInOrder()
    {
        var entry = new TestCatalogEntry { ItemId = "1" };
        var (manager, handlerMock) = CreateManagerWithHandler();
        await manager.ValidateAsync(entry);
        handlerMock.Verify(h => h.ValidatingAsync(It.Is<ValidatingContext<TestCatalogEntry>>(ctx => ctx.Model == entry)), Times.Once);
        handlerMock.Verify(h => h.ValidatedAsync(It.Is<ValidatedContext<TestCatalogEntry>>(ctx => ctx.Model == entry)), Times.Once);
    }

    [Fact]
    public async Task NewAsync_InvokesHandlersInOrder()
    {
        var (manager, handlerMock) = CreateManagerWithHandler();
        var entry = await manager.NewAsync();
        handlerMock.Verify(h => h.InitializingAsync(It.Is<InitializingContext<TestCatalogEntry>>(ctx => ctx.Model == entry)), Times.Once);
        handlerMock.Verify(h => h.InitializedAsync(It.Is<InitializedContext<TestCatalogEntry>>(ctx => ctx.Model == entry)), Times.Once);
    }

    [Fact]
    public async Task FindByIdAsync_InvokesLoadedHandler()
    {
        var entry = new TestCatalogEntry { ItemId = "1" };
        var (manager, handlerMock) = CreateManagerWithHandler([entry]);
        await manager.FindByIdAsync("1");
        handlerMock.Verify(h => h.LoadedAsync(It.Is<LoadedContext<TestCatalogEntry>>(ctx => ctx.Model == entry)), Times.Once);
    }

    [Fact]
    public async Task GetAllAsync_InvokesLoadedHandlerForEach()
    {
        var entries = new List<TestCatalogEntry> { new() { ItemId = "1" }, new() { ItemId = "2" } };
        var (manager, handlerMock) = CreateManagerWithHandler(entries);
        await manager.GetAllAsync();
        handlerMock.Verify(h => h.LoadedAsync(It.IsAny<LoadedContext<TestCatalogEntry>>()), Times.Exactly(2));
    }

    [Fact]
    public async Task DeleteAsync_InvokesHandlersInCorrectOrderAndTiming()
    {
        var entry = new TestCatalogEntry { ItemId = "1" };
        var records = new List<TestCatalogEntry> { entry };
        var catalog = FakeDocumentManager.CreateCatalog(records, out _);
        var logger = Mock.Of<ILogger<CatalogManager<TestCatalogEntry>>>();
        var callOrder = new Queue<string>();
        var existsInCatalogDuringDeleting = false;
        var existsInCatalogDuringDeleted = false;

        var handler = new TestCatalogEntryHandler<TestCatalogEntry>
        {
            OnDeletingAsync = async ctx =>
            {
                // Check via catalog, not records list
                existsInCatalogDuringDeleting = await catalog.FindByIdAsync(entry.ItemId) != null;
                callOrder.Enqueue("DeletingAsync");
            },
            OnDeletedAsync = async ctx =>
            {
                existsInCatalogDuringDeleted = await catalog.FindByIdAsync(entry.ItemId) != null;
                callOrder.Enqueue("DeletedAsync");
            }
        };

        var manager = new CatalogManager<TestCatalogEntry>(catalog, [handler], logger);
        await manager.DeleteAsync(entry);

        Assert.Equal("DeletingAsync", callOrder.Dequeue());
        Assert.Equal("DeletedAsync", callOrder.Dequeue());
        Assert.Empty(callOrder);
        Assert.True(existsInCatalogDuringDeleting); // Should exist before deletion
        Assert.False(existsInCatalogDuringDeleted); // Should not exist after deletion
    }

    [Fact]
    public async Task CreateAsync_InvokesHandlersInCorrectOrderAndTiming()
    {
        var entry = new TestCatalogEntry { ItemId = "new" };
        var records = new List<TestCatalogEntry>();
        var catalog = FakeDocumentManager.CreateCatalog(records, out _);
        var logger = Mock.Of<ILogger<CatalogManager<TestCatalogEntry>>>();
        var callOrder = new Queue<string>();
        var existsInCatalogDuringCreating = false;
        var existsInCatalogDuringCreated = false;

        var handler = new TestCatalogEntryHandler<TestCatalogEntry>
        {
            OnCreatingAsync = async ctx =>
            {
                existsInCatalogDuringCreating = await catalog.FindByIdAsync(entry.ItemId) == null;
                callOrder.Enqueue("CreatingAsync");
            },
            OnCreatedAsync = async ctx =>
            {
                existsInCatalogDuringCreated = await catalog.FindByIdAsync(entry.ItemId) != null;
                callOrder.Enqueue("CreatedAsync");
            }
        };

        var manager = new CatalogManager<TestCatalogEntry>(catalog, [handler], logger);
        await manager.CreateAsync(entry);

        Assert.Equal("CreatingAsync", callOrder.Dequeue());
        Assert.Equal("CreatedAsync", callOrder.Dequeue());
        Assert.Empty(callOrder);
        Assert.True(existsInCatalogDuringCreating); // Should not exist before creation
        Assert.True(existsInCatalogDuringCreated); // Should exist after creation
    }

    [Fact]
    public async Task UpdateAsync_InvokesHandlersInCorrectOrderAndTiming()
    {
        var entry = new TestCatalogEntry { ItemId = "1" };
        var records = new List<TestCatalogEntry> { entry };
        var catalog = FakeDocumentManager.CreateCatalog(records, out _);
        var logger = Mock.Of<ILogger<CatalogManager<TestCatalogEntry>>>();
        var callOrder = new Queue<string>();
        var existsInCatalogDuringUpdating = false;
        var existsInCatalogDuringUpdated = false;

        var handler = new TestCatalogEntryHandler<TestCatalogEntry>
        {
            OnUpdatingAsync = async ctx =>
            {
                existsInCatalogDuringUpdating = await catalog.FindByIdAsync(entry.ItemId) != null;
                callOrder.Enqueue("UpdatingAsync");
            },
            OnUpdatedAsync = async ctx =>
            {
                existsInCatalogDuringUpdated = await catalog.FindByIdAsync(entry.ItemId) != null;
                callOrder.Enqueue("UpdatedAsync");
            }
        };

        var manager = new CatalogManager<TestCatalogEntry>(catalog, [handler], logger);
        await manager.UpdateAsync(entry);

        Assert.Equal("UpdatingAsync", callOrder.Dequeue());
        Assert.Equal("UpdatedAsync", callOrder.Dequeue());
        Assert.Empty(callOrder);
        Assert.True(existsInCatalogDuringUpdating); // Should exist before update
        Assert.True(existsInCatalogDuringUpdated); // Should exist after update
    }

    [Fact]
    public async Task ValidateAsync_InvokesHandlersInCorrectOrderAndTiming()
    {
        var entry = new TestCatalogEntry { ItemId = "1" };
        var records = new List<TestCatalogEntry> { entry };
        var catalog = FakeDocumentManager.CreateCatalog(records, out _);
        var logger = Mock.Of<ILogger<CatalogManager<TestCatalogEntry>>>();
        var callOrder = new Queue<string>();
        var existsInCatalogDuringValidating = false;
        var existsInCatalogDuringValidated = false;

        var handler = new TestCatalogEntryHandler<TestCatalogEntry>
        {
            OnValidatingAsync = async ctx =>
            {
                existsInCatalogDuringValidating = await catalog.FindByIdAsync(entry.ItemId) != null;
                callOrder.Enqueue("ValidatingAsync");
            },
            OnValidatedAsync = async ctx =>
            {
                existsInCatalogDuringValidated = await catalog.FindByIdAsync(entry.ItemId) != null;
                callOrder.Enqueue("ValidatedAsync");
            }
        };

        var manager = new CatalogManager<TestCatalogEntry>(catalog, [handler], logger);
        await manager.ValidateAsync(entry);

        Assert.Equal("ValidatingAsync", callOrder.Dequeue());
        Assert.Equal("ValidatedAsync", callOrder.Dequeue());
        Assert.Empty(callOrder);
        Assert.True(existsInCatalogDuringValidating); // Should exist before validating
        Assert.True(existsInCatalogDuringValidated); // Should exist after validated
    }

    [Fact]
    public async Task NewAsync_InvokesHandlersInCorrectOrderAndTiming()
    {
        var records = new List<TestCatalogEntry>();
        var catalog = FakeDocumentManager.CreateCatalog(records, out _);
        var logger = Mock.Of<ILogger<CatalogManager<TestCatalogEntry>>>();
        var callOrder = new Queue<string>();
        var entryIdDuringInitializing = string.Empty;
        var entryIdDuringInitialized = string.Empty;

        var handler = new TestCatalogEntryHandler<TestCatalogEntry>
        {
            OnInitializingAsync = ctx =>
            {
                entryIdDuringInitializing = ctx.Model.ItemId;
                callOrder.Enqueue("InitializingAsync");

                return Task.CompletedTask;
            },
            OnInitializedAsync = ctx =>
            {
                entryIdDuringInitialized = ctx.Model.ItemId;
                callOrder.Enqueue("InitializedAsync");

                return Task.CompletedTask;
            }
        };

        var manager = new CatalogManager<TestCatalogEntry>(catalog, [handler], logger);
        var entry = await manager.NewAsync();

        Assert.Equal("InitializingAsync", callOrder.Dequeue());
        Assert.Equal("InitializedAsync", callOrder.Dequeue());
        Assert.Empty(callOrder);
        Assert.False(string.IsNullOrEmpty(entryIdDuringInitializing));
        Assert.False(string.IsNullOrEmpty(entryIdDuringInitialized));
        Assert.Equal(entryIdDuringInitializing, entryIdDuringInitialized);
    }

    [Fact]
    public async Task FindByIdAsync_InvokesHandlersInCorrectOrderAndTiming()
    {
        var entry = new TestCatalogEntry { ItemId = "1" };
        var records = new List<TestCatalogEntry> { entry };
        var catalog = FakeDocumentManager.CreateCatalog(records, out _);
        var logger = Mock.Of<ILogger<CatalogManager<TestCatalogEntry>>>();
        var callOrder = new Queue<string>();
        var existsInCatalogDuringLoaded = false;

        var handler = new TestCatalogEntryHandler<TestCatalogEntry>
        {
            OnLoadedAsync = async ctx =>
            {
                existsInCatalogDuringLoaded = await catalog.FindByIdAsync(entry.ItemId) != null;
                callOrder.Enqueue("LoadedAsync");
            }
        };

        var manager = new CatalogManager<TestCatalogEntry>(catalog, [handler], logger);
        await manager.FindByIdAsync("1");

        Assert.Equal("LoadedAsync", callOrder.Dequeue());
        Assert.Empty(callOrder);
        Assert.True(existsInCatalogDuringLoaded); // Should exist when loaded
    }

    [Fact]
    public async Task PageAsync_InvokesHandlersInCorrectOrderAndTiming()
    {
        var entries = Enumerable.Range(1, 5).Select(i => new TestCatalogEntry { ItemId = i.ToString() }).ToList();
        var catalog = FakeDocumentManager.CreateCatalog(entries, out _);
        var logger = Mock.Of<ILogger<CatalogManager<TestCatalogEntry>>>();
        var callOrder = new Queue<string>();
        var loadedIds = new List<string>();

        var handler = new TestCatalogEntryHandler<TestCatalogEntry>
        {
            OnLoadedAsync = ctx =>
            {
                loadedIds.Add(ctx.Model.ItemId);
                callOrder.Enqueue($"LoadedAsync:{ctx.Model.ItemId}");
                return Task.CompletedTask;
            }
        };

        var manager = new CatalogManager<TestCatalogEntry>(catalog, [handler], logger);
        var context = new QueryContext();
        var result = await manager.PageAsync(1, 5, context);

        foreach (var entry in entries)
        {
            Assert.Equal($"LoadedAsync:{entry.ItemId}", callOrder.Dequeue());
        }
        Assert.Empty(callOrder);
        Assert.Equal(entries.Select(e => e.ItemId), loadedIds);
    }

    [Fact]
    public async Task GetAllAsync_InvokesHandlersInCorrectOrderAndTiming()
    {
        var entries = new List<TestCatalogEntry>
        {
            new() { ItemId = "1" },
            new() { ItemId = "2" },
            new() { ItemId = "3" }
        };

        var catalog = FakeDocumentManager.CreateCatalog(entries, out _);
        var logger = Mock.Of<ILogger<CatalogManager<TestCatalogEntry>>>();
        var callOrder = new Queue<string>();
        var loadedIds = new List<string>();

        var handler = new TestCatalogEntryHandler<TestCatalogEntry>
        {
            OnLoadedAsync = ctx =>
            {
                loadedIds.Add(ctx.Model.ItemId);
                callOrder.Enqueue($"LoadedAsync:{ctx.Model.ItemId}");
                return Task.CompletedTask;
            }
        };

        var manager = new CatalogManager<TestCatalogEntry>(catalog, [handler], logger);
        var result = (await manager.GetAllAsync()).ToList();

        foreach (var entry in entries)
        {
            Assert.Equal($"LoadedAsync:{entry.ItemId}", callOrder.Dequeue());
        }
        Assert.Empty(callOrder);
        Assert.Equal(entries.Select(e => e.ItemId), loadedIds);
    }
}
