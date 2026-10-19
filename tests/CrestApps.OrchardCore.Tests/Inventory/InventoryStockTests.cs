using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Inventory.Core.Indexes;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.Inventory.Migrations;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
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

    public async ValueTask InitializeAsync()
    {
        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_path};Pooling=False"));
        _store.RegisterIndexes([new StockItemIndexProvider(), new StockMovementIndexProvider(), new StockReservationIndexProvider()], InventoryConstants.CollectionName);
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

    private static DefaultInventoryService CreateService(ISession session)
        => new(session, new StockItemStore(session), new StockMovementStore(session), Clock());

    // One unit of work, committed, as a request or a checkout would.
    private async Task<T> InSessionAsync<T>(Func<DefaultInventoryService, Task<T>> work)
    {
        await using var session = _store.CreateSession();
        var result = await work(CreateService(session));
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        return result;
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

        Assert.True((await InSessionAsync(service => service.AdjustAsync(itemId, StockMovementKind.Received, 10, "Delivery 42", _admin, TestContext.Current.CancellationToken))).Succeeded);

        var counted = await InSessionAsync(service => service.SetCountAsync(itemId, 9, "Shelf count", _admin, TestContext.Current.CancellationToken));
        Assert.Equal(-1, counted.Movement.Quantity);
        Assert.Equal(9, counted.Item.OnHand);

        var damaged = await InSessionAsync(service => service.AdjustAsync(itemId, StockMovementKind.Adjusted, -2, "Damaged", _admin, TestContext.Current.CancellationToken));
        Assert.Equal(7, damaged.Item.OnHand);

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

        await InSessionAsync(service => service.AdjustAsync(itemId, StockMovementKind.Received, 10, null, _admin, TestContext.Current.CancellationToken));
        await InSessionAsync(service => service.AdjustAsync(itemId, StockMovementKind.Adjusted, -3, null, _admin, TestContext.Current.CancellationToken));

        // Something wrote a wrong count without a movement.
        await using (var session = _store.CreateSession())
        {
            var store = new StockItemStore(session);
            var item = await store.FindByIdAsync(itemId, TestContext.Current.CancellationToken);
            item.OnHand = 99;
            await store.UpdateAsync(item, TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var rebuilt = await InSessionAsync(service => service.RebuildAsync(itemId, TestContext.Current.CancellationToken));

        Assert.Equal(7, rebuilt.Item.OnHand);
    }

    [Theory]
    [InlineData(StockMovementKind.Received, 0)]
    [InlineData(StockMovementKind.Received, -5)]
    [InlineData(StockMovementKind.Adjusted, 0)]
    [InlineData(StockMovementKind.Sold, 1)]
    public async Task AnAdjustmentThatChangesNothingOrIsNotAnAdjustment_IsRefused(StockMovementKind kind, int quantity)
    {
        var itemId = await AddItemAsync();

        var result = await InSessionAsync(service => service.AdjustAsync(itemId, kind, quantity, null, _admin, TestContext.Current.CancellationToken));

        Assert.Equal(StockChangeError.InvalidQuantity, result.Error);
    }

    [Fact]
    public async Task ANegativeCount_AndAMissingItem_AreRefused()
    {
        var itemId = await AddItemAsync();

        Assert.Equal(StockChangeError.InvalidQuantity, (await InSessionAsync(service => service.SetCountAsync(itemId, -1, null, _admin, TestContext.Current.CancellationToken))).Error);
        Assert.Equal(StockChangeError.NotFound, (await InSessionAsync(service => service.SetCountAsync("missing", 3, null, _admin, TestContext.Current.CancellationToken))).Error);
    }

    [Fact]
    public async Task TwoCheckoutsHoldingTheLastUnitAtTheSameMoment_OnlyTheFirstToCommitKeepsIt()
    {
        var itemId = await AddItemAsync();
        await InSessionAsync(service => service.SetCountAsync(itemId, 1, null, _admin, TestContext.Current.CancellationToken));

        // Both read the item before either commits.
        await using var first = _store.CreateSession();
        await using var second = _store.CreateSession();
        Assert.True((await CreateService(first).ReserveAsync("order-a", [new StockRequest("mug", null, 1)], _now.AddMinutes(60), TestContext.Current.CancellationToken)).Succeeded);
        Assert.True((await CreateService(second).ReserveAsync("order-b", [new StockRequest("mug", null, 1)], _now.AddMinutes(60), TestContext.Current.CancellationToken)).Succeeded);

        await first.SaveChangesAsync(TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<ConcurrencyException>(() => second.SaveChangesAsync(TestContext.Current.CancellationToken));

        var item = await FindAsync(itemId);
        Assert.Equal(1, item.Reserved);
        Assert.Equal(0, item.Available);
    }

    [Fact]
    public async Task TheList_FiltersLowOutAndBackordered_AndFindsBySkuOrTitle()
    {
        var low = await AddItemAsync("mug", threshold: 5, title: "Mug", sku: "MUG-1");
        var plenty = await AddItemAsync("lamp", threshold: 5, title: "Lamp", sku: "LAMP-1");
        var gone = await AddItemAsync("vase", threshold: 5, title: "Vase", sku: "VASE-1");
        var backordered = await AddItemAsync("bowl", threshold: 5, title: "Bowl", sku: "BOWL-1");

        await InSessionAsync(service => service.SetCountAsync(low, 3, null, _admin, TestContext.Current.CancellationToken));
        await InSessionAsync(service => service.SetCountAsync(plenty, 40, null, _admin, TestContext.Current.CancellationToken));
        await InSessionAsync(service => service.SetCountAsync(gone, 0, null, _admin, TestContext.Current.CancellationToken));
        await InSessionAsync(service => service.AdjustAsync(backordered, StockMovementKind.Adjusted, -2, null, _admin, TestContext.Current.CancellationToken));

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

    [Fact]
    public async Task TwoBuyersRacingForTheLastUnit_OneHoldsIt_AndTheOtherIsToldItSoldOut()
    {
        var itemId = await AddItemAsync();
        await StockedServiceAsync(itemId, 1);
        var expires = _now.AddMinutes(60);

        var first = await InSessionAsync(service => service.ReserveAsync("order-a", [new StockRequest("mug", null, 1)], expires, TestContext.Current.CancellationToken));
        var second = await InSessionAsync(service => service.ReserveAsync("order-b", [new StockRequest("mug", null, 1)], expires, TestContext.Current.CancellationToken));

        Assert.True(first.Succeeded);
        var shortage = Assert.Single(second.Shortages);
        Assert.Equal(0, shortage.Available);
        Assert.Equal("Mug", shortage.Item.Title);

        var item = await FindAsync(itemId);
        Assert.Equal(1, item.OnHand);
        Assert.Equal(1, item.Reserved);
        Assert.Equal(0, item.Available);
    }

    [Fact]
    public async Task AnOrderShortOfOneItem_HoldsNothing()
    {
        var mug = await AddItemAsync("mug");
        var lamp = await AddItemAsync("lamp", title: "Lamp", sku: "LAMP");
        await StockedServiceAsync(mug, 5);
        await InSessionAsync(service => service.SetCountAsync(lamp, 1, null, _admin, TestContext.Current.CancellationToken));

        var result = await InSessionAsync(service => service.ReserveAsync("order-a", [new StockRequest("mug", null, 2), new StockRequest("lamp", null, 3)], _now.AddMinutes(60), TestContext.Current.CancellationToken));

        Assert.False(result.Succeeded);
        Assert.Equal(1, Assert.Single(result.Shortages).Available);
        Assert.Equal(0, (await FindAsync(mug)).Reserved);
        Assert.Equal(0, (await FindAsync(lamp)).Reserved);
    }

    [Fact]
    public async Task BackordersAndUncountedProducts_AreNeverShort()
    {
        var itemId = await AddItemAsync();

        await using (var session = _store.CreateSession())
        {
            var store = new StockItemStore(session);
            var item = await store.FindByIdAsync(itemId, TestContext.Current.CancellationToken);
            item.AllowBackorder = true;
            await store.UpdateAsync(item, TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var result = await InSessionAsync(service => service.ReserveAsync("order-a", [new StockRequest("mug", null, 4), new StockRequest("not-counted", null, 9)], _now.AddMinutes(60), TestContext.Current.CancellationToken));

        Assert.True(result.Succeeded);
        Assert.Equal(-4, (await FindAsync(itemId)).Available);
    }

    [Fact]
    public async Task PayingSellsWhatWasHeld_Once()
    {
        var itemId = await AddItemAsync();
        await StockedServiceAsync(itemId, 5);
        await InSessionAsync(service => service.ReserveAsync("order-a", [new StockRequest("mug", null, 2)], _now.AddMinutes(60), TestContext.Current.CancellationToken));

        await InSessionAsync(service => service.CommitAsync("order-a", [new StockRequest("mug", null, 2)], TestContext.Current.CancellationToken));
        await InSessionAsync(service => service.CommitAsync("order-a", [new StockRequest("mug", null, 2)], TestContext.Current.CancellationToken));

        var item = await FindAsync(itemId);
        Assert.Equal(3, item.OnHand);
        Assert.Equal(0, item.Reserved);

        var movements = await MovementsAsync(itemId);
        var sold = Assert.Single(movements, movement => movement.Kind == StockMovementKind.Sold);
        Assert.Equal(-2, sold.Quantity);
        Assert.Equal("order-a", sold.OrderId);
        Assert.Equal(3, sold.OnHandAfter);
    }

    [Fact]
    public async Task ARefundedOrder_PutsWhatItSoldBackInStock_Once()
    {
        var itemId = await AddItemAsync();
        await StockedServiceAsync(itemId, 5);
        await InSessionAsync(service => service.ReserveAsync("order-a", [new StockRequest("mug", null, 2)], _now.AddMinutes(60), TestContext.Current.CancellationToken));
        await InSessionAsync(service => service.CommitAsync("order-a", [new StockRequest("mug", null, 2)], TestContext.Current.CancellationToken));

        var first = await InSessionAsync(service => service.RestockAsync("order-a", "Order 261018-ABC was refunded.", _admin, TestContext.Current.CancellationToken));
        var second = await InSessionAsync(service => service.RestockAsync("order-a", "Again", _admin, TestContext.Current.CancellationToken));

        Assert.Equal(2, first);
        Assert.Equal(0, second);
        Assert.Equal(5, (await FindAsync(itemId)).OnHand);

        var returned = Assert.Single(await MovementsAsync(itemId), movement => movement.Kind == StockMovementKind.Returned);
        Assert.Equal(2, returned.Quantity);
        Assert.Equal("order-a", returned.OrderId);
        Assert.Equal("admin", returned.ActorName);
    }

    [Fact]
    public async Task AnOrderPaidAfterItsHoldEnded_IsSoldAnyway_Once()
    {
        var itemId = await AddItemAsync();
        await StockedServiceAsync(itemId, 1);
        await InSessionAsync(service => service.ReserveAsync("order-a", [new StockRequest("mug", null, 1)], _now.AddMinutes(60), TestContext.Current.CancellationToken));
        await InSessionAsync(service => service.ReleaseAsync("order-a", "replaced", TestContext.Current.CancellationToken));

        // Someone else bought the unit meanwhile.
        await InSessionAsync(service => service.ReserveAsync("order-b", [new StockRequest("mug", null, 1)], _now.AddMinutes(60), TestContext.Current.CancellationToken));
        await InSessionAsync(service => service.CommitAsync("order-b", [new StockRequest("mug", null, 1)], TestContext.Current.CancellationToken));

        await InSessionAsync(service => service.CommitAsync("order-a", [new StockRequest("mug", null, 1)], TestContext.Current.CancellationToken));
        await InSessionAsync(service => service.CommitAsync("order-a", [new StockRequest("mug", null, 1)], TestContext.Current.CancellationToken));

        var item = await FindAsync(itemId);
        Assert.Equal(-1, item.OnHand);
        Assert.Equal(0, item.Reserved);
        Assert.Equal(2, (await MovementsAsync(itemId)).Count(movement => movement.Kind == StockMovementKind.Sold));
    }

    [Fact]
    public async Task CancelingLetsTheHoldGo_AndExpiredHoldsAreLetGoBySweeping()
    {
        var itemId = await AddItemAsync();
        await StockedServiceAsync(itemId, 10);

        await InSessionAsync(service => service.ReserveAsync("canceled", [new StockRequest("mug", null, 2)], _now.AddMinutes(60), TestContext.Current.CancellationToken));
        await InSessionAsync(service => service.ReserveAsync("expired", [new StockRequest("mug", null, 3)], _now.AddMinutes(-1), TestContext.Current.CancellationToken));
        await InSessionAsync(service => service.ReserveAsync("open", [new StockRequest("mug", null, 4)], _now.AddMinutes(60), TestContext.Current.CancellationToken));
        Assert.Equal(9, (await FindAsync(itemId)).Reserved);

        await InSessionAsync(service => service.ReleaseAsync("canceled", "canceled", TestContext.Current.CancellationToken));
        await InSessionAsync(service => service.ReleaseAsync("canceled", "canceled", TestContext.Current.CancellationToken));
        Assert.Equal(7, (await FindAsync(itemId)).Reserved);

        Assert.Equal(1, await InSessionAsync(service => service.ReleaseExpiredAsync(100, TestContext.Current.CancellationToken)));
        Assert.Equal(4, (await FindAsync(itemId)).Reserved);

        // Nothing was sold, so nothing on hand changed.
        Assert.Equal(10, (await FindAsync(itemId)).OnHand);
    }

    [Fact]
    public async Task RebuildingRecountsTheHeldUnitsToo()
    {
        var itemId = await AddItemAsync();
        await StockedServiceAsync(itemId, 10);
        await InSessionAsync(service => service.ReserveAsync("order-a", [new StockRequest("mug", null, 3)], _now.AddMinutes(60), TestContext.Current.CancellationToken));

        await using (var session = _store.CreateSession())
        {
            var store = new StockItemStore(session);
            var item = await store.FindByIdAsync(itemId, TestContext.Current.CancellationToken);
            item.Reserved = 0;
            await store.UpdateAsync(item, TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var rebuilt = await InSessionAsync(service => service.RebuildAsync(itemId, TestContext.Current.CancellationToken));

        Assert.Equal(3, rebuilt.Item.Reserved);
    }

    [Fact]
    public async Task Availability_ListsAProductsVariants_AndCountsACartsOwnHoldsAsItsOwn()
    {
        var red = await AddItemAsync("shirt", "red", threshold: 2, title: "Shirt (Red)", sku: "RED");
        var blue = await AddItemAsync("shirt", "blue", threshold: 2, title: "Shirt (Blue)", sku: "BLUE");
        await StockedServiceAsync(red, 3);
        await StockedServiceAsync(blue, 1);

        // The buyer started checkout: their unpaid order holds 2 red shirts.
        await InSessionAsync(service => service.ReserveAsync("order-a", [new StockRequest("shirt", "red", 2)], _now.AddMinutes(60), TestContext.Current.CancellationToken));

        var orders = new Mock<CrestApps.OrchardCore.Orders.Core.Services.IOrderStore>();
        orders
            .Setup(store => store.ListUnpaidByCartAsync("cart-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CrestApps.OrchardCore.Orders.Core.Models.Order { ItemId = "order-a" }]);

        var services = new ServiceCollection()
            .AddSingleton(orders.Object)
            .BuildServiceProvider();

        await using var session = _store.CreateSession();
        var provider = new CrestApps.OrchardCore.Inventory.Services.InventoryAvailabilityProvider(session, services, Clock());

        var everyone = await provider.GetAvailabilityAsync(new CrestApps.OrchardCore.Products.Core.Services.ProductAvailabilityRequest
        {
            Products = [new CrestApps.OrchardCore.Products.Core.Services.ProductStockKey("shirt", null)],
        }, TestContext.Current.CancellationToken);

        Assert.Equal(2, everyone.Count);
        Assert.Equal(1, everyone[new("shirt", "red")].Available);
        Assert.Equal(CrestApps.OrchardCore.Products.Core.Services.ProductAvailabilityStatus.LowStock, everyone[new("shirt", "blue")].Status);

        var theBuyer = await provider.GetAvailabilityAsync(new CrestApps.OrchardCore.Products.Core.Services.ProductAvailabilityRequest
        {
            Products = [new CrestApps.OrchardCore.Products.Core.Services.ProductStockKey("shirt", "red")],
            CartId = "cart-1",
        }, TestContext.Current.CancellationToken);

        Assert.Equal(3, Assert.Single(theBuyer).Value.Available);
    }

    private Task<StockChangeResult> StockedServiceAsync(string itemId, int onHand)
        => InSessionAsync(service => service.SetCountAsync(itemId, onHand, null, _admin, TestContext.Current.CancellationToken));

    private async Task<StockItem> FindAsync(string itemId)
    {
        await using var session = _store.CreateSession();

        return await new StockItemStore(session).FindByIdAsync(itemId, TestContext.Current.CancellationToken);
    }

    private async Task<IReadOnlyList<StockMovement>> MovementsAsync(string itemId)
    {
        await using var session = _store.CreateSession();

        return (await new StockMovementStore(session).PageAsync(itemId, 0, 100, TestContext.Current.CancellationToken)).Movements;
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
