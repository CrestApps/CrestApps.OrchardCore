using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Inventory.Core.Indexes;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.Inventory.Migrations;
using CrestApps.OrchardCore.Inventory.Services;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Tests.Utilities;
using Moq;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Entities;
using OrchardCore.Modules;
using OrchardCore.Settings;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Inventory;

/// <summary>
/// Stock locations live in a store: the ones kept in the settings move there with their ids, a van's location is
/// owned by its vehicle and never fulfills orders, and parts used on a work order come off the van with a reference.
/// </summary>
public sealed class StockLocationStoreTests : IAsyncLifetime
{
    private static readonly DateTime _now = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);
    private static readonly StockActor _tech = new("user-7", "tech");

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"crestapps-stock-locations-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_path};Pooling=False"));
        _store.RegisterIndexes([new StockItemIndexProvider(), new StockMovementIndexProvider(), new StockReservationIndexProvider(), new StockLocationIndexProvider()], InventoryConstants.CollectionName);
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
    public async Task LocationsKeptInTheSettings_MoveToTheStore_WithTheirIds_Once()
    {
        InventoryLocation[] legacy = [new() { LocationId = "shop-1", Name = "Downtown shop", Address = "1 Main St" }, new() { LocationId = InventoryConstants.DefaultLocationId, Name = "Warehouse" }, new() { LocationId = "annex", Name = "Annex" }];

        Assert.Equal(3, await InSessionAsync(session => StockLocationImporter.ImportAsync(legacy, new StockLocationStore(session), _now)));
        Assert.Equal(0, await InSessionAsync(session => StockLocationImporter.ImportAsync(legacy, new StockLocationStore(session), _now)));

        var (stored, listed) = await InSessionAsync(async session =>
        {
            var store = new StockLocationStore(session);

            return ((await store.GetAllAsync()).OrderBy(location => location.ItemId).ToList(), await new InventoryLocationService(Site(new InventorySettings()), store).GetLocationsAsync());
        });

        Assert.Equal(["annex", "default", "shop-1"], stored.Select(location => location.ItemId));
        Assert.Equal("1 Main St", stored[2].Address);

        // Orders still take units from them in the order they had in the settings.
        Assert.Equal(["shop-1", "default", "annex"], listed.Select(location => location.LocationId));
    }

    [Fact]
    public async Task AVansLocation_IsALocation_ButNeverFulfillsOrders()
    {
        await InSessionAsync(async session =>
        {
            var store = new StockLocationStore(session);

            await store.CreateAsync(new StockLocation { ItemId = "van-12", Name = "Van 12", OwnerKind = "Asset", OwnerId = "asset-12" });
            await store.CreateAsync(new StockLocation { ItemId = "shop-1", Name = "Shop" });

            return true;
        });

        var (all, fulfilling, owned) = await InSessionAsync(async session =>
        {
            var store = new StockLocationStore(session);
            var service = new InventoryLocationService(Site(new InventorySettings { Locations = [new() { LocationId = "legacy", Name = "Old annex" }] }), store);

            return (await service.GetLocationsAsync(), await ((IFulfillmentLocationProvider)service).GetLocationsAsync(), await store.FindByOwnerAsync("Asset", "asset-12"));
        });

        // The default comes first, then the site's own, then owned ones; a location still in the settings is kept.
        Assert.Equal([InventoryConstants.DefaultLocationId, "shop-1", "van-12", "legacy"], all.Select(location => location.LocationId));
        Assert.Equal([InventoryConstants.DefaultLocationId, "shop-1", "legacy"], fulfilling.Select(location => location.LocationId));
        Assert.Equal("van-12", owned.ItemId);
    }

    [Fact]
    public async Task PartsUsedOnAWorkOrder_ComeOffTheVan_WithAReference()
    {
        await InSessionAsync(async session =>
        {
            await new StockItemStore(session).CreateAsync(new StockItem { ItemId = "filter-item", ContentItemId = "filter", Title = "Filter", Sku = "FLT" });

            return true;
        });

        await InSessionAsync(session => Service(session).AdjustAsync("filter-item", "van-12", StockMovementKind.Received, 5, "Loaded", _tech, TestContext.Current.CancellationToken));
        await InSessionAsync(session => Service(session).ReserveAsync("order-1", [new StockRequest("filter", null, 3, "van-12")], _now.AddHours(1), TestContext.Current.CancellationToken));

        var tooMany = await InSessionAsync(session => Service(session).ConsumeAsync("filter-item", "van-12", 3, "WorkOrder", "wo-1", null, _tech, TestContext.Current.CancellationToken));
        var used = await InSessionAsync(session => Service(session).ConsumeAsync("filter-item", "van-12", 2, "WorkOrder", "wo-1", "Fitted", _tech, TestContext.Current.CancellationToken));
        var none = await InSessionAsync(session => Service(session).ConsumeAsync("filter-item", "van-12", 0, "WorkOrder", "wo-1", null, _tech, TestContext.Current.CancellationToken));

        Assert.Equal(StockChangeError.NotEnoughAtLocation, tooMany.Error);
        Assert.Equal(StockChangeError.InvalidQuantity, none.Error);
        Assert.True(used.Succeeded);
        Assert.Equal(3, used.Item.FindLevel("van-12").OnHand);

        var movement = await InSessionAsync(session => session.Query<StockMovement, StockMovementIndex>(index => index.ReferenceType == "WorkOrder" && index.ReferenceId == "wo-1", collection: InventoryConstants.CollectionName).FirstOrDefaultAsync(TestContext.Current.CancellationToken));

        Assert.Equal(StockMovementKind.Consumed, movement.Kind);
        Assert.Equal(-2, movement.Quantity);
        Assert.Equal("van-12", movement.LocationId);
        Assert.Equal("Fitted", movement.Reason);
    }

    private static DefaultInventoryService Service(ISession session)
    {
        var clock = new Mock<IClock>();

        clock.Setup(value => value.UtcNow).Returns(_now);

        var locations = new Mock<IInventoryLocationService>();

        locations.Setup(service => service.GetLocationsAsync()).ReturnsAsync([new InventoryLocation { LocationId = InventoryConstants.DefaultLocationId, Name = "Warehouse" }, new InventoryLocation { LocationId = "van-12", Name = "Van 12", OwnerKind = "Asset", OwnerId = "asset-12" }]);

        return new(session, new StockItemStore(session), new StockMovementStore(session), clock.Object, locations: locations.Object);
    }

    private static ISiteService Site(InventorySettings settings)
    {
        var site = new SiteSettings();
        var service = new Mock<ISiteService>();

        site.Put(settings);
        service.Setup(s => s.GetSiteSettingsAsync()).ReturnsAsync(site);

        return service.Object;
    }

    private async Task<T> InSessionAsync<T>(Func<ISession, Task<T>> work)
    {
        await using var session = _store.CreateSession();
        var result = await work(session);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        return result;
    }
}
