using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Inventory.Core.Indexes;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.Inventory.Migrations;
using CrestApps.OrchardCore.Tests.Utilities;
using Moq;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Modules;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Inventory;

/// <summary>
/// Stock is kept per location: an order takes its units from one location that has them all, trying the one it asked
/// for first; transfers move units without changing the total; and returned units go back where they were sold from.
/// </summary>
public sealed class InventoryLocationTests : IAsyncLifetime
{
    private const string Warehouse = InventoryConstants.DefaultLocationId;
    private const string Shop = "shop";

    private static readonly DateTime _now = new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);
    private static readonly StockActor _admin = new("user-1", "admin");

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"crestapps-inventory-locations-{Guid.NewGuid():N}.db");
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

    [Fact]
    public async Task ReceivingAtALocation_AddsToItsUnits_AndTheTotal()
    {
        var itemId = await AddItemAsync();

        await InSessionAsync(service => service.AdjustAsync(itemId, Warehouse, StockMovementKind.Received, 10, null, _admin, TestContext.Current.CancellationToken));
        await InSessionAsync(service => service.SetCountAsync(itemId, Shop, 3, "Shelf count", _admin, TestContext.Current.CancellationToken));

        var item = await FindAsync(itemId);

        Assert.Equal(13, item.OnHand);
        Assert.Equal(10, item.FindLevel(Warehouse).OnHand);
        Assert.Equal(3, item.FindLevel(Shop).OnHand);
        Assert.Equal([Warehouse, Shop], (await MovementsAsync(itemId)).Select(movement => movement.LocationId));
    }

    [Fact]
    public async Task ATransfer_MovesOnlyAvailableUnits_AndKeepsTheTotal()
    {
        var itemId = await StockAsync(warehouse: 10, shop: 0);

        await InSessionAsync(service => service.ReserveAsync("order-1", [new StockRequest("mug", null, 8)], _now.AddHours(1), TestContext.Current.CancellationToken));

        var tooMany = await InSessionAsync(service => service.TransferAsync(itemId, Warehouse, Shop, 3, null, _admin, TestContext.Current.CancellationToken));
        var toItself = await InSessionAsync(service => service.TransferAsync(itemId, Shop, Shop, 1, null, _admin, TestContext.Current.CancellationToken));
        var moved = await InSessionAsync(service => service.TransferAsync(itemId, Warehouse, Shop, 2, "Restock the shop", _admin, TestContext.Current.CancellationToken));

        Assert.Equal(StockChangeError.NotEnoughAtLocation, tooMany.Error);
        Assert.Equal(StockChangeError.NotEnoughAtLocation, toItself.Error);
        Assert.True(moved.Succeeded);

        var item = await FindAsync(itemId);

        Assert.Equal(10, item.OnHand);
        Assert.Equal((8, 8), (item.FindLevel(Warehouse).OnHand, item.FindLevel(Warehouse).Reserved));
        Assert.Equal(2, item.FindLevel(Shop).OnHand);
        Assert.Equal([-2, 2], (await MovementsAsync(itemId)).Where(movement => movement.Kind == StockMovementKind.Transferred).Select(movement => movement.Quantity));
    }

    [Fact]
    public async Task AnOrder_TakesItsUnitsFromOneLocationThatHasThemAll_ElseFromEachInTurn()
    {
        var itemId = await StockAsync(warehouse: 2, shop: 5);

        await InSessionAsync(service => service.ReserveAsync("whole", [new StockRequest("mug", null, 4)], _now.AddHours(1), TestContext.Current.CancellationToken));
        await InSessionAsync(service => service.ReserveAsync("split", [new StockRequest("mug", null, 3)], _now.AddHours(1), TestContext.Current.CancellationToken));

        var item = await FindAsync(itemId);

        // 4 fit only in the shop; then 3 fit nowhere whole, so 2 come from the warehouse and 1 from the shop.
        Assert.Equal(2, item.FindLevel(Warehouse).Reserved);
        Assert.Equal(5, item.FindLevel(Shop).Reserved);
        Assert.Equal(7, item.Reserved);
    }

    [Fact]
    public async Task AnOrderPickedUpAtAShop_IsSoldFromTheShop_WhenItHasTheUnits_AndRestockedThere()
    {
        var itemId = await StockAsync(warehouse: 10, shop: 3);

        // Held from the warehouse when the order was made, before the buyer chose to pick it up at the shop.
        await InSessionAsync(service => service.ReserveAsync("order-1", [new StockRequest("mug", null, 2)], _now.AddHours(1), TestContext.Current.CancellationToken));
        Assert.Equal(2, (await FindAsync(itemId)).FindLevel(Warehouse).Reserved);

        await InSessionAsync(service => service.CommitAsync("order-1", [new StockRequest("mug", null, 2, Shop)], TestContext.Current.CancellationToken));

        var sold = await FindAsync(itemId);

        Assert.Equal((10, 0), (sold.FindLevel(Warehouse).OnHand, sold.FindLevel(Warehouse).Reserved));
        Assert.Equal((1, 0), (sold.FindLevel(Shop).OnHand, sold.FindLevel(Shop).Reserved));

        await InSessionAsync(service => service.RestockAsync("order-1", "Returned", _admin, TestContext.Current.CancellationToken));

        Assert.Equal(3, (await FindAsync(itemId)).FindLevel(Shop).OnHand);
    }

    [Fact]
    public async Task AnItemCountedBeforeLocations_HasItsUnitsAtTheDefaultLocation_AndRebuildsPerLocation()
    {
        var itemId = await AddItemAsync(onHand: 6, reserved: 1);

        var legacy = await FindAsync(itemId);

        Assert.Empty(legacy.Levels);
        Assert.Equal((6, 1), (legacy.GetLevel(null).OnHand, legacy.GetLevel(null).Reserved));

        await InSessionAsync(service => service.AdjustAsync(itemId, Shop, StockMovementKind.Received, 4, null, _admin, TestContext.Current.CancellationToken));

        var item = await FindAsync(itemId);

        Assert.Equal(10, item.OnHand);
        Assert.Equal(6, item.FindLevel(Warehouse).OnHand);

        // The legacy units have no movement, so a rebuild from the history keeps only what was recorded.
        var rebuilt = (await InSessionAsync(service => service.RebuildAsync(itemId, TestContext.Current.CancellationToken))).Item;

        Assert.Equal(4, rebuilt.OnHand);
        Assert.Equal(4, rebuilt.FindLevel(Shop).OnHand);
    }

    [Fact]
    public async Task AReturnOfSomeUnits_GoesBackWhereAsked_AndARestockLaterReturnsOnlyTheRest()
    {
        var itemId = await StockAsync(warehouse: 10, shop: 0);

        await InSessionAsync(service => service.CommitAsync("order-1", [new StockRequest("mug", null, 4)], TestContext.Current.CancellationToken));

        // Two come back to the shop; asking for more than was sold returns only what's left.
        Assert.Equal(2, await InSessionAsync(service => service.RestockAsync("order-1", [new StockRequest("mug", null, 2)], Shop, "Return R-1", _admin, TestContext.Current.CancellationToken)));
        Assert.Equal(2, await InSessionAsync(service => service.RestockAsync("order-1", [new StockRequest("mug", null, 9)], null, "Return R-2", _admin, TestContext.Current.CancellationToken)));
        Assert.Equal(0, await InSessionAsync(service => service.RestockAsync("order-1", "Refunded", _admin, TestContext.Current.CancellationToken)));

        var item = await FindAsync(itemId);

        Assert.Equal(10, item.OnHand);
        Assert.Equal(8, item.FindLevel(Warehouse).OnHand);
        Assert.Equal(2, item.FindLevel(Shop).OnHand);
    }

    [Fact]
    public void ABackorder_IsOwedByThePreferredLocation()
    {
        var item = new StockItem { AllowBackorder = true, Levels = [new() { LocationId = Warehouse, OnHand = 1 }, new() { LocationId = Shop, OnHand = 1 }], OnHand = 2 };

        Assert.Equal([(Shop, 2), (Warehouse, 1)], DefaultInventoryService.Allocate(item, 3, Shop, [Warehouse, Shop]));
    }

    private static IClock Clock()
    {
        var clock = new Mock<IClock>();
        clock.Setup(value => value.UtcNow).Returns(_now);

        return clock.Object;
    }

    private static DefaultInventoryService CreateService(ISession session)
    {
        var locations = new Mock<IInventoryLocationService>();
        locations.Setup(service => service.GetLocationsAsync()).ReturnsAsync([new InventoryLocation { LocationId = Warehouse, Name = "Warehouse" }, new InventoryLocation { LocationId = Shop, Name = "Shop" }]);

        return new(session, new StockItemStore(session), new StockMovementStore(session), Clock(), locations: locations.Object);
    }

    private async Task<T> InSessionAsync<T>(Func<DefaultInventoryService, Task<T>> work)
    {
        await using var session = _store.CreateSession();
        var result = await work(CreateService(session));
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        return result;
    }

    private async Task<string> StockAsync(int warehouse, int shop)
    {
        var itemId = await AddItemAsync();

        if (warehouse > 0)
        {
            await InSessionAsync(service => service.AdjustAsync(itemId, Warehouse, StockMovementKind.Received, warehouse, null, _admin, TestContext.Current.CancellationToken));
        }

        if (shop > 0)
        {
            await InSessionAsync(service => service.AdjustAsync(itemId, Shop, StockMovementKind.Received, shop, null, _admin, TestContext.Current.CancellationToken));
        }

        return itemId;
    }

    private async Task<string> AddItemAsync(int onHand = 0, int reserved = 0)
    {
        await using var session = _store.CreateSession();
        var item = new StockItem { ItemId = "mug-item", ContentItemId = "mug", Title = "Mug", Sku = "MUG", OnHand = onHand, Reserved = reserved };

        await new StockItemStore(session).CreateAsync(item, TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        return item.ItemId;
    }

    private async Task<StockItem> FindAsync(string itemId)
    {
        await using var session = _store.CreateSession();

        return await new StockItemStore(session).FindByIdAsync(itemId, TestContext.Current.CancellationToken);
    }

    private async Task<IReadOnlyList<StockMovement>> MovementsAsync(string itemId)
    {
        await using var session = _store.CreateSession();

        return (await new StockMovementStore(session).PageAsync(itemId, 0, 100, TestContext.Current.CancellationToken)).Movements.Reverse().ToArray();
    }
}
