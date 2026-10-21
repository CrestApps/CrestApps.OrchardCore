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
/// A purchase order's stock is received at its location, never more than is still to come, and the order is received
/// once nothing is.
/// </summary>
public sealed class PurchaseOrderReceiverTests : IAsyncLifetime
{
    private const string Shop = "shop";

    private static readonly DateTime _now = new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);
    private static readonly StockActor _admin = new("user-1", "admin");

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"crestapps-purchase-orders-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_path};Pooling=False"));
        _store.RegisterIndexes([new StockItemIndexProvider(), new StockMovementIndexProvider(), new StockReservationIndexProvider(), new PurchaseOrderIndexProvider()], InventoryConstants.CollectionName);
        await _store.InitializeAsync(TestContext.Current.CancellationToken);
        await _store.InitializeCollectionAsync(InventoryConstants.CollectionName, TestContext.Current.CancellationToken);

        await using var session = _store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await new InventoryMigrations(Mock.Of<IContentDefinitionManager>()) { SchemaBuilder = new SchemaBuilder(_store.Configuration, transaction) }.CreateAsync();
        await new PurchaseOrderMigrations { SchemaBuilder = new SchemaBuilder(_store.Configuration, transaction) }.CreateAsync();
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _path);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task ReceivingAddsStockAtTheOrdersLocation_CappedAtWhatsOutstanding_UntilTheOrderIsReceived()
    {
        await AddItemAsync("mug");
        await AddItemAsync("cap");

        var order = new PurchaseOrder
        {
            ItemId = "po-1",
            Number = "PO-261019-TEST",
            LocationId = Shop,
            Status = PurchaseOrderStatus.Ordered,
            Lines =
            [
                new() { LineId = "a", StockItemId = "mug-item", Title = "Mug", Ordered = 10 },
                new() { LineId = "b", StockItemId = "cap-item", Title = "Cap", Ordered = 4 },
            ],
        };

        await using (var session = _store.CreateSession())
        {
            await new PurchaseOrderStore(session).CreateAsync(order, TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal(6, await ReceiveAsync(new Dictionary<string, int> { ["a"] = 6 }));
        Assert.Equal(PurchaseOrderStatus.PartiallyReceived, (await FindOrderAsync()).Status);

        // More than is still to come is capped; a line not posted is left alone.
        Assert.Equal(8, await ReceiveAsync(new Dictionary<string, int> { ["a"] = 9, ["b"] = 4 }));

        var received = await FindOrderAsync();

        Assert.Equal(PurchaseOrderStatus.Received, received.Status);
        Assert.Equal(_now, received.ReceivedUtc);
        Assert.Equal(0, await ReceiveAsync(new Dictionary<string, int> { ["a"] = 1 }));

        await using var check = _store.CreateSession();
        var mug = await new StockItemStore(check).FindByIdAsync("mug-item", TestContext.Current.CancellationToken);
        var movements = (await new StockMovementStore(check).PageAsync("mug-item", 0, 10, TestContext.Current.CancellationToken)).Movements;

        Assert.Equal(10, mug.FindLevel(Shop).OnHand);
        Assert.All(movements, movement => Assert.Equal(("po-1", Shop, "PO-261019-TEST"), (movement.PurchaseOrderId, movement.LocationId, movement.Reason)));
    }

    private async Task<int> ReceiveAsync(IReadOnlyDictionary<string, int> quantities)
    {
        await using var session = _store.CreateSession();
        var orders = new PurchaseOrderStore(session);
        var order = await orders.FindByIdAsync("po-1", TestContext.Current.CancellationToken);
        var inventory = new DefaultInventoryService(session, new StockItemStore(session), new StockMovementStore(session), Clock());

        var received = await new PurchaseOrderReceiver(inventory, orders, Clock()).ReceiveAsync(order, quantities, _admin, TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        return received;
    }

    private async Task<PurchaseOrder> FindOrderAsync()
    {
        await using var session = _store.CreateSession();

        return await new PurchaseOrderStore(session).FindByIdAsync("po-1", TestContext.Current.CancellationToken);
    }

    private async Task AddItemAsync(string contentItemId)
    {
        await using var session = _store.CreateSession();
        await new StockItemStore(session).CreateAsync(new StockItem { ItemId = contentItemId + "-item", ContentItemId = contentItemId, Title = contentItemId }, TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static IClock Clock()
    {
        var clock = new Mock<IClock>();
        clock.Setup(value => value.UtcNow).Returns(_now);

        return clock.Object;
    }
}
