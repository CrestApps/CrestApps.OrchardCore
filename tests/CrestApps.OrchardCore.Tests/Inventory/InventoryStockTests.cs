using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Inventory.Core.Indexes;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.Inventory.Migrations;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Tests.Utilities;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Inventory;

/// <summary>
/// Stock is a ledger: every change is a movement, the units on hand are the sum of the movements, and a product's
/// stock items follow its variants.
/// </summary>
public sealed class InventoryStockTests : IAsyncLifetime
{
    private static readonly DateTime _now = new(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);
    private static readonly StockActor _admin = new("user-1", "admin");

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"crestapps-inventory-{Guid.NewGuid():N}.db");
    private IStore _store;
    private bool _lockGranted = true;

    public async ValueTask InitializeAsync()
    {
        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_path};Pooling=False"));
        _store.RegisterIndexes([new StockItemIndexProvider(), new StockMovementIndexProvider()], InventoryConstants.CollectionName);
        await _store.InitializeAsync(TestContext.Current.CancellationToken);
        await _store.InitializeCollectionAsync(InventoryConstants.CollectionName, TestContext.Current.CancellationToken);

        await using var session = _store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await new InventoryMigrations(Mock.Of<IContentDefinitionManager>()) { SchemaBuilder = new SchemaBuilder(_store.Configuration, transaction) }.CreateAsync();
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _path);

        return ValueTask.CompletedTask;
    }

    private static IClock Clock()
    {
        var clock = new Mock<IClock>();
        clock.Setup(value => value.UtcNow).Returns(_now);

        return clock.Object;
    }

    private DefaultInventoryService CreateService(ISession session)
    {
        var distributedLock = new Mock<IDistributedLock>();
        distributedLock
            .Setup(locking => locking.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync(() => (Mock.Of<ILocker>(), _lockGranted));

        return new DefaultInventoryService(new StockItemStore(session), new StockMovementStore(session), distributedLock.Object, session, Clock());
    }

    private async Task<string> AddItemAsync(string contentItemId = "mug", string variantId = null, int threshold = 5, string title = "Mug", string sku = "MUG")
    {
        await using var session = _store.CreateSession();
        var item = new StockItem
        {
            ItemId = contentItemId + "-" + (variantId ?? "item"),
            ContentItemId = contentItemId,
            VariantId = variantId,
            Title = title,
            Sku = sku,
            LowStockThreshold = threshold,
        };

        await new StockItemStore(session).CreateAsync(item, TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        return item.ItemId;
    }

    [Fact]
    public async Task ReceivingCountingAndAdjusting_AreEachAMovement_AndTheHistoryExplainsTheCount()
    {
        var itemId = await AddItemAsync();

        await using (var session = _store.CreateSession())
        {
            var service = CreateService(session);

            Assert.True((await service.AdjustAsync(itemId, StockMovementKind.Received, 10, "Delivery 42", _admin, TestContext.Current.CancellationToken)).Succeeded);

            var counted = await service.SetCountAsync(itemId, 9, "Shelf count", _admin, TestContext.Current.CancellationToken);
            Assert.Equal(-1, counted.Movement.Quantity);
            Assert.Equal(9, counted.Item.OnHand);

            var damaged = await service.AdjustAsync(itemId, StockMovementKind.Adjusted, -2, "Damaged", _admin, TestContext.Current.CancellationToken);
            Assert.Equal(7, damaged.Item.OnHand);
        }

        await using (var session = _store.CreateSession())
        {
            var item = await new StockItemStore(session).FindByIdAsync(itemId, TestContext.Current.CancellationToken);
            var (count, movements) = await new StockMovementStore(session).PageAsync(itemId, 0, 10, TestContext.Current.CancellationToken);

            Assert.Equal(7, item.OnHand);
            Assert.Equal(3, count);

            // Newest first, each saying what was on hand after it, who made it and why.
            Assert.Equal([StockMovementKind.Adjusted, StockMovementKind.Counted, StockMovementKind.Received], movements.Select(movement => movement.Kind));
            Assert.Equal([7, 9, 10], movements.Select(movement => movement.OnHandAfter));
            Assert.All(movements, movement => Assert.Equal("admin", movement.ActorName));
            Assert.Equal("Damaged", movements[0].Reason);
        }
    }

    [Fact]
    public async Task TheCountOnHand_CanBeRebuiltFromTheHistory()
    {
        var itemId = await AddItemAsync();

        await using (var session = _store.CreateSession())
        {
            var service = CreateService(session);
            await service.AdjustAsync(itemId, StockMovementKind.Received, 10, null, _admin, TestContext.Current.CancellationToken);
            await service.AdjustAsync(itemId, StockMovementKind.Adjusted, -3, null, _admin, TestContext.Current.CancellationToken);
        }

        // Something wrote a wrong count without a movement.
        await using (var session = _store.CreateSession())
        {
            var store = new StockItemStore(session);
            var item = await store.FindByIdAsync(itemId, TestContext.Current.CancellationToken);
            item.OnHand = 99;
            await store.UpdateAsync(item, TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var session = _store.CreateSession())
        {
            var rebuilt = await CreateService(session).RebuildAsync(itemId, TestContext.Current.CancellationToken);

            Assert.Equal(7, rebuilt.Item.OnHand);
        }
    }

    [Theory]
    [InlineData(StockMovementKind.Received, 0)]
    [InlineData(StockMovementKind.Received, -5)]
    [InlineData(StockMovementKind.Adjusted, 0)]
    [InlineData(StockMovementKind.Sold, 1)]
    public async Task AnAdjustmentThatChangesNothingOrIsNotAnAdjustment_IsRefused(StockMovementKind kind, int quantity)
    {
        var itemId = await AddItemAsync();

        await using var session = _store.CreateSession();

        var result = await CreateService(session).AdjustAsync(itemId, kind, quantity, null, _admin, TestContext.Current.CancellationToken);

        Assert.Equal(StockChangeError.InvalidQuantity, result.Error);
    }

    [Fact]
    public async Task ANegativeCount_AMissingItem_AndABusyItem_AreRefused()
    {
        var itemId = await AddItemAsync();

        await using var session = _store.CreateSession();
        var service = CreateService(session);

        Assert.Equal(StockChangeError.InvalidQuantity, (await service.SetCountAsync(itemId, -1, null, _admin, TestContext.Current.CancellationToken)).Error);
        Assert.Equal(StockChangeError.NotFound, (await service.SetCountAsync("missing", 3, null, _admin, TestContext.Current.CancellationToken)).Error);

        _lockGranted = false;
        Assert.Equal(StockChangeError.Busy, (await service.SetCountAsync(itemId, 3, null, _admin, TestContext.Current.CancellationToken)).Error);
    }

    [Fact]
    public async Task TheList_FiltersLowOutAndBackordered_AndFindsBySkuOrTitle()
    {
        var low = await AddItemAsync("mug", threshold: 5, title: "Mug", sku: "MUG-1");
        var plenty = await AddItemAsync("lamp", threshold: 5, title: "Lamp", sku: "LAMP-1");
        var gone = await AddItemAsync("vase", threshold: 5, title: "Vase", sku: "VASE-1");
        var backordered = await AddItemAsync("bowl", threshold: 5, title: "Bowl", sku: "BOWL-1");

        await using (var session = _store.CreateSession())
        {
            var service = CreateService(session);
            await service.SetCountAsync(low, 3, null, _admin, TestContext.Current.CancellationToken);
            await service.SetCountAsync(plenty, 40, null, _admin, TestContext.Current.CancellationToken);
            await service.SetCountAsync(gone, 0, null, _admin, TestContext.Current.CancellationToken);
            await service.AdjustAsync(backordered, StockMovementKind.Adjusted, -2, null, _admin, TestContext.Current.CancellationToken);
        }

        await using (var session = _store.CreateSession())
        {
            var store = new StockItemStore(session);

            async Task<string[]> Ids(StockItemQuery query)
                => (await store.PageAsync(query, 0, 50, TestContext.Current.CancellationToken)).Items.Select(item => item.ItemId).ToArray();

            // The fewest available first.
            Assert.Equal([backordered, gone, low, plenty], await Ids(new StockItemQuery()));
            Assert.Equal([backordered, gone, low], await Ids(new StockItemQuery { Filter = StockItemFilter.Low }));
            Assert.Equal([backordered, gone], await Ids(new StockItemQuery { Filter = StockItemFilter.Out }));
            Assert.Equal([backordered], await Ids(new StockItemQuery { Filter = StockItemFilter.Backordered }));
            Assert.Equal([plenty], await Ids(new StockItemQuery { Search = "lamp" }));
            Assert.Equal([low], await Ids(new StockItemQuery { Search = "mug-" }));
        }
    }

    [Fact]
    public async Task AProductsStockItems_FollowItsVariants_AndRemovedOnesAreArchived()
    {
        var settings = new InventorySettings { DefaultLowStockThreshold = 4 };
        var shirt = CreateShirt(("red", "Red", "SHIRT-RED"), ("blue", "Blue", null));

        await SyncAsync(shirt, settings);

        var items = await ListAsync("shirt");
        Assert.Equal(2, items.Count);

        var red = items.Single(item => item.VariantId == "red");
        Assert.Equal("Shirt (Red)", red.Title);
        Assert.Equal("SHIRT-RED", red.Sku);
        Assert.Equal(4, red.LowStockThreshold);

        // A variant without its own SKU is found by the product's.
        Assert.Equal("SHIRT", items.Single(item => item.VariantId == "blue").Sku);

        // Blue goes; its stock item is archived with its history, not deleted.
        await SyncAsync(CreateShirt(("red", "Red", "SHIRT-RED")), settings);

        items = await ListAsync("shirt");
        Assert.True(items.Single(item => item.VariantId == "blue").Archived);
        Assert.False(items.Single(item => item.VariantId == "red").Archived);

        // Blue comes back: the same stock item, counted again.
        await SyncAsync(shirt, settings);
        Assert.Equal(2, (await ListAsync("shirt")).Count(item => !item.Archived));
    }

    [Fact]
    public async Task AProductWithoutVariants_HasOneStockItem_AndStopsBeingCountedWhenTrackingIsOff()
    {
        var mug = new ContentItem { ContentType = "Product", ContentItemId = "mug", DisplayText = "Mug" };
        mug.Apply(nameof(ProductPart), new ProductPart { Sku = "MUG" });
        mug.Apply(nameof(InventoryPart), new InventoryPart { LowStockThreshold = 2, AllowBackorder = true });

        await SyncAsync(mug, new InventorySettings());

        var item = Assert.Single(await ListAsync("mug"));
        Assert.Null(item.VariantId);
        Assert.Equal(2, item.LowStockThreshold);
        Assert.True(item.AllowBackorder);

        mug.Apply(nameof(InventoryPart), new InventoryPart { TrackInventory = false });
        await SyncAsync(mug, new InventorySettings());

        Assert.True(Assert.Single(await ListAsync("mug")).Archived);
    }

    [Fact]
    public async Task AProductSavedBeforeItsTypeHadThePart_IsCountedWithTheDefaults_AndAGoneProductIsArchived()
    {
        var mug = new ContentItem { ContentType = "Product", ContentItemId = "mug", DisplayText = "Mug" };
        mug.Apply(nameof(ProductPart), new ProductPart { Sku = "MUG" });

        await SyncAsync(mug, new InventorySettings { DefaultLowStockThreshold = 7 });

        Assert.Equal(7, Assert.Single(await ListAsync("mug")).LowStockThreshold);

        await using (var session = _store.CreateSession())
        {
            await new InventoryProductSynchronizer(new StockItemStore(session), Clock()).SyncAsync("mug", product: null, typeHasInventory: true, new InventorySettings(), TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Assert.True(Assert.Single(await ListAsync("mug")).Archived);
    }

    private async Task SyncAsync(ContentItem product, InventorySettings settings)
    {
        await using var session = _store.CreateSession();
        await new InventoryProductSynchronizer(new StockItemStore(session), Clock()).SyncAsync(product.ContentItemId, product, typeHasInventory: true, settings, TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<IReadOnlyList<StockItem>> ListAsync(string contentItemId)
    {
        await using var session = _store.CreateSession();

        return await new StockItemStore(session).ListByProductAsync(contentItemId, TestContext.Current.CancellationToken);
    }

    private static ContentItem CreateShirt(params (string Id, string Color, string Sku)[] variants)
    {
        var shirt = new ContentItem { ContentType = "Shirt", ContentItemId = "shirt", DisplayText = "Shirt" };

        shirt.Apply(nameof(ProductPart), new ProductPart { Sku = "SHIRT" });
        shirt.Apply(nameof(ProductVariantsPart), new ProductVariantsPart
        {
            Options = [new ProductOption { Name = "Color", Values = [.. variants.Select(variant => variant.Color)] }],
            Variants = [.. variants.Select(variant => new ProductVariant
            {
                VariantId = variant.Id,
                Sku = variant.Sku,
                Options = new(StringComparer.OrdinalIgnoreCase) { ["Color"] = variant.Color },
            })],
        });

        return shirt;
    }
}
