using System.Text.Json;
using CrestApps.OrchardCore.Inventory;
using CrestApps.OrchardCore.Inventory.AI;
using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Inventory.Core.Indexes;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.Inventory.Migrations;
using CrestApps.OrchardCore.Inventory.Services;
using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;
using YesSql.Provider.Sqlite;
using YesSql.Sql;
using ISession = YesSql.ISession;
using IStore = YesSql.IStore;
using StoreFactory = YesSql.StoreFactory;

namespace CrestApps.OrchardCore.Tests.Inventory.AI;

/// <summary>
/// The inventory tools check the Inventory pages' permissions first, change stock through the inventory service with the
/// caller and a reason on every movement, commit each change at once, refuse unlisted locations, import counts all or
/// nothing, and edit the stock locations as the Inventory settings page does.
/// </summary>
public sealed class InventoryToolTests : IAsyncLifetime
{
    private const string Shop = "shop";

    private static readonly DateTime _now = new(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"crestapps-inventory-tools-{Guid.NewGuid():N}.db");
    private readonly InventorySettings _settings = new();
    private IStore _store;

    public static TheoryData<string, string> ToolsAndPermissions => new()
    {
        { ListStockItemsTool.TheName, InventoryPermissions.ViewInventory.Name },
        { GetStockItemTool.TheName, InventoryPermissions.ViewInventory.Name },
        { AdjustStockTool.TheName, InventoryPermissions.ManageInventory.Name },
        { TransferStockTool.TheName, InventoryPermissions.ManageInventory.Name },
        { ImportStockCountsTool.TheName, InventoryPermissions.ManageInventory.Name },
        { SyncInventoryTool.TheName, InventoryPermissions.ManageInventory.Name },
        { ListStockLocationsTool.TheName, InventoryPermissions.ViewInventory.Name },
        { SaveStockLocationTool.TheName, InventoryPermissions.ManageInventory.Name },
        { DeleteStockLocationTool.TheName, InventoryPermissions.ManageInventory.Name },
    };

    public async ValueTask InitializeAsync()
    {
        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_path};Pooling=False"));
        _store.RegisterIndexes([new StockItemIndexProvider(), new StockMovementIndexProvider(), new StockReservationIndexProvider(), new StockLocationIndexProvider()], InventoryConstants.CollectionName);
        await _store.InitializeAsync(TestContext.Current.CancellationToken);
        await _store.InitializeCollectionAsync(InventoryConstants.CollectionName, TestContext.Current.CancellationToken);

        await using (var session = _store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
            await new InventoryMigrations(Mock.Of<IContentDefinitionManager>()) { SchemaBuilder = new SchemaBuilder(_store.Configuration, transaction) }.CreateAsync();
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        await using (var session = _store.CreateSession())
        {
            var locations = new StockLocationStore(session);

            await locations.CreateAsync(new StockLocation { ItemId = InventoryConstants.DefaultLocationId, Name = "Warehouse", Position = 1 }, TestContext.Current.CancellationToken);
            await locations.CreateAsync(new StockLocation { ItemId = Shop, Name = "Downtown shop", Position = 2 }, TestContext.Current.CancellationToken);
            await locations.CreateAsync(new StockLocation { ItemId = "van-1", Name = "Van 1", OwnerKind = "Asset", OwnerId = "asset-1" }, TestContext.Current.CancellationToken);

            var items = new StockItemStore(session);

            await items.CreateAsync(new StockItem { ItemId = "mug", ContentItemId = "mug-product", Sku = "MUG-BLUE", Title = "Blue mug", LowStockThreshold = 5 }, TestContext.Current.CancellationToken);
            await items.CreateAsync(new StockItem { ItemId = "candle", ContentItemId = "candle-product", Sku = "CANDLE-01", Title = "Candle", LowStockThreshold = 2 }, TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var session = _store.CreateSession())
        {
            var service = new DefaultInventoryService(session, new StockItemStore(session), new StockMovementStore(session), Clock());

            await service.AdjustAsync("mug", InventoryConstants.DefaultLocationId, StockMovementKind.Received, 20, "Opening stock", new StockActor("u-1", "setup"), TestContext.Current.CancellationToken);
            await service.AdjustAsync("candle", InventoryConstants.DefaultLocationId, StockMovementKind.Received, 1, "Opening stock", new StockActor("u-1", "setup"), TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _path);

        return ValueTask.CompletedTask;
    }

    [Theory]
    [MemberData(nameof(ToolsAndPermissions))]
    public async Task Tool_RefusesACallerWhoIsNotSignedIn(string toolName, string permission)
    {
        // Act
        var result = await InvokeAsync(toolName, Build(signedIn: false), Arguments(toolName));

        // Assert
        Assert.Equal(InventoryAIToolBase.NotAllowedMessage, result);
        Assert.NotNull(permission);
        Assert.Equal(20, (await FindAsync("mug")).OnHand);
    }

    [Theory]
    [MemberData(nameof(ToolsAndPermissions))]
    public async Task Tool_RefusesACallerWithoutThePermission(string toolName, string permission)
    {
        // Arrange
        var services = Build(authorize: (asked, _) => asked.Name != permission);

        // Act
        var result = await InvokeAsync(toolName, services, Arguments(toolName));

        // Assert
        Assert.Equal(InventoryAIToolBase.NotAllowedMessage, result);
        Assert.Equal(permission, Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks).Permission);
        Assert.Equal(20, (await FindAsync("mug")).OnHand);
        Assert.Equal(3, (await LocationsAsync()).Count);
    }

    [Fact]
    public async Task ListStockItems_ListsAndFiltersAsTheInventoryPage()
    {
        // Act
        var all = AgentToolTestContext.ReadJson(await InvokeAsync(ListStockItemsTool.TheName, Build(), []));
        var low = AgentToolTestContext.ReadJson(await InvokeAsync(ListStockItemsTool.TheName, Build(), new() { ["filter"] = "low" }));
        var wrong = await InvokeAsync(ListStockItemsTool.TheName, Build(), new() { ["filter"] = "empty" });

        // Assert
        Assert.Equal(2, all.GetProperty("total").GetInt32());
        Assert.Equal("CANDLE-01", Assert.Single(low.GetProperty("items").EnumerateArray()).GetProperty("sku").GetString());
        Assert.StartsWith("Nothing was listed: filter must be", wrong, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetStockItem_FindsBySku_WithLevelsAndHistory_AndSaysWhenNothingMatches()
    {
        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(GetStockItemTool.TheName, Build(), new() { ["sku"] = "mug-blue" }));
        var missing = await InvokeAsync(GetStockItemTool.TheName, Build(), new() { ["sku"] = "NOPE" });

        // Assert
        var level = Assert.Single(json.GetProperty("item").GetProperty("levels").EnumerateArray());
        var movement = Assert.Single(json.GetProperty("movements").EnumerateArray());

        Assert.Equal("Warehouse", level.GetProperty("location").GetString());
        Assert.Equal(20, level.GetProperty("onHand").GetInt32());
        Assert.Equal("Opening stock", movement.GetProperty("reason").GetString());
        Assert.Equal("Received", movement.GetProperty("kind").GetString());
        Assert.Contains("or more than one shares it", missing, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdjustStock_ReceivesAndCounts_RecordingTheCallerAndTheReason()
    {
        // Act
        var received = AgentToolTestContext.ReadJson(await InvokeAsync(AdjustStockTool.TheName, Build(), new() { ["stockItemId"] = "mug", ["kind"] = "receive", ["quantity"] = 40, ["locationId"] = Shop, ["reason"] = "Delivery 42" }));
        var counted = AgentToolTestContext.ReadJson(await InvokeAsync(AdjustStockTool.TheName, Build(), new() { ["stockItemId"] = "mug", ["kind"] = "count", ["quantity"] = 12, ["locationId"] = Shop, ["reason"] = "Shelf count" }));

        // Assert
        var item = await FindAsync("mug");

        Assert.Equal("admin", received.GetProperty("movement").GetProperty("by").GetString());
        Assert.Equal("Delivery 42", received.GetProperty("movement").GetProperty("reason").GetString());
        Assert.Equal(-28, counted.GetProperty("movement").GetProperty("quantity").GetInt32());
        Assert.Equal(32, item.OnHand);
        Assert.Equal(12, item.FindLevel(Shop).OnHand);
    }

    [Theory]
    [InlineData("receive", 0, "default", "Delivery", "more than zero")]
    [InlineData("adjust", 0, "default", "Damaged", "not zero")]
    [InlineData("count", -1, "default", "Count", "zero or more")]
    [InlineData("receive", 5, "default", null, "say why the stock changes")]
    [InlineData("receive", 5, "nowhere", "Delivery", "there is no stock location with the id 'nowhere'")]
    [InlineData("move", 5, "default", "Delivery", "kind must be receive, adjust or count")]
    public async Task AdjustStock_RefusesAWrongChange_AndChangesNothing(string kind, int quantity, string locationId, string reason, string expected)
    {
        // Act
        var result = await InvokeAsync(AdjustStockTool.TheName, Build(), new() { ["stockItemId"] = "mug", ["kind"] = kind, ["quantity"] = quantity, ["locationId"] = locationId, ["reason"] = reason });

        // Assert
        Assert.Contains(expected, result, StringComparison.Ordinal);
        Assert.Equal(20, (await FindAsync("mug")).OnHand);
    }

    [Fact]
    public async Task TransferStock_MovesAvailableUnits_ToAListedLocationOnly()
    {
        // Act
        var moved = AgentToolTestContext.ReadJson(await InvokeAsync(TransferStockTool.TheName, Build(), new() { ["stockItemId"] = "mug", ["fromLocationId"] = "default", ["toLocationId"] = Shop, ["quantity"] = 5 }));
        var unlisted = await InvokeAsync(TransferStockTool.TheName, Build(), new() { ["stockItemId"] = "mug", ["fromLocationId"] = "default", ["toLocationId"] = "attic", ["quantity"] = 1 });
        var tooMany = await InvokeAsync(TransferStockTool.TheName, Build(), new() { ["stockItemId"] = "mug", ["fromLocationId"] = "default", ["toLocationId"] = Shop, ["quantity"] = 99 });

        // Assert
        var item = await FindAsync("mug");

        Assert.Equal("From Warehouse to Downtown shop", moved.GetProperty("reason").GetString());
        Assert.Equal(20, item.OnHand);
        Assert.Equal(5, item.FindLevel(Shop).OnHand);
        Assert.StartsWith("Nothing was changed: there is no listed stock location with the id 'attic'", unlisted, StringComparison.Ordinal);
        Assert.Equal("Nothing was changed: Warehouse doesn't have 99 available to move. Units held for orders stay where they are.", tooMany);
    }

    [Fact]
    public async Task ImportStockCounts_IsAllOrNothing_AndCountsEveryRowWhenAllAreRight()
    {
        // Act
        var refused = AgentToolTestContext.ReadJson(await InvokeAsync(ImportStockCountsTool.TheName, Build(), new()
        {
            ["rows"] = Rows("""[{ "sku": "MUG-BLUE", "count": 3 }, { "sku": "NOPE", "count": 1 }, { "sku": "mug-blue", "count": 4 }]"""),
            ["reason"] = "Stocktake",
        }));
        var unchanged = (await FindAsync("mug")).OnHand;
        var counted = AgentToolTestContext.ReadJson(await InvokeAsync(ImportStockCountsTool.TheName, Build(), new()
        {
            ["rows"] = Rows("""[{ "sku": "MUG-BLUE", "count": 18 }, { "contentItemId": "candle-product", "count": 1 }]"""),
            ["reason"] = "Stocktake",
        }));

        // Assert
        Assert.False(refused.GetProperty("changed").GetBoolean());
        Assert.Equal(2, refused.GetProperty("problemCount").GetInt32());
        Assert.Equal(20, unchanged);
        Assert.Equal(1, counted.GetProperty("counted").GetInt32());
        Assert.Equal(1, counted.GetProperty("unchanged").GetInt32());
        Assert.Equal(18, (await FindAsync("mug")).OnHand);
    }

    [Fact]
    public async Task AStockChangeThatLosesARace_IsBusy_AndLaterChangesInTheMessageAreRefused()
    {
        // Arrange: two units of work change the same stock item; the second to commit loses.
        await using var first = _store.CreateSession();
        await using var second = _store.CreateSession();
        var firstChange = await new DefaultInventoryService(first, new StockItemStore(first), new StockMovementStore(first), Clock()).AdjustAsync("mug", StockMovementKind.Adjusted, -1, "Broken", new StockActor("u-1", "one"), TestContext.Current.CancellationToken);
        var secondChange = await new DefaultInventoryService(second, new StockItemStore(second), new StockMovementStore(second), Clock()).AdjustAsync("mug", StockMovementKind.Adjusted, -2, "Broken", new StockActor("u-2", "two"), TestContext.Current.CancellationToken);

        // Act
        var won = await StockChanges.CommitAsync(first, firstChange, TestContext.Current.CancellationToken);
        var lost = await StockChanges.CommitAsync(second, secondChange, TestContext.Current.CancellationToken);

        var services = Build();
        services.GetRequiredService<InventoryToolScope>().SessionClosed = true;
        var later = await InvokeAsync(AdjustStockTool.TheName, services, new() { ["stockItemId"] = "mug", ["kind"] = "receive", ["quantity"] = 1, ["reason"] = "Delivery" });

        // Assert
        Assert.True(won.Succeeded);
        Assert.Equal(StockChangeError.Busy, lost.Error);
        Assert.Equal(19, (await FindAsync("mug")).OnHand);
        Assert.StartsWith("Nothing was changed: an earlier change in this message was refused", later, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListStockLocations_ListsTheSitesOwnFirst_ThenTheOwnedOnes()
    {
        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(ListStockLocationsTool.TheName, Build(), []));

        // Assert
        var items = json.GetProperty("items").EnumerateArray().ToArray();

        Assert.Equal(["default", Shop, "van-1"], items.Select(item => item.GetProperty("locationId").GetString()));
        Assert.Equal("Asset", items[2].GetProperty("ownedBy").GetString());
        Assert.Equal(2, items[1].GetProperty("position").GetInt32());
    }

    [Fact]
    public async Task SaveStockLocation_AddsALocation_AndRenamesAndReordersOne_KeepingTheirIds()
    {
        // Act
        var added = AgentToolTestContext.ReadJson(await InvokeAndSaveAsync(SaveStockLocationTool.TheName, new() { ["name"] = "Airport kiosk", ["address"] = "Terminal 2" }));
        var moved = AgentToolTestContext.ReadJson(await InvokeAndSaveAsync(SaveStockLocationTool.TheName, new() { ["locationId"] = Shop, ["name"] = "Main street shop", ["position"] = 1 }));
        var owned = await InvokeAndSaveAsync(SaveStockLocationTool.TheName, new() { ["locationId"] = "van-1", ["name"] = "Van" });
        var nameless = await InvokeAndSaveAsync(SaveStockLocationTool.TheName, new() { ["address"] = "Somewhere" });

        // Assert
        var locations = await LocationsAsync();
        var newId = added.GetProperty("locationId").GetString();

        Assert.True(added.GetProperty("created").GetBoolean());
        Assert.Equal(1, moved.GetProperty("position").GetInt32());
        Assert.Equal([Shop, "default", newId], locations.Where(location => location.OwnerKind is null).OrderBy(location => location.Position).Select(location => location.ItemId));
        Assert.Equal("Main street shop", locations.Single(location => location.ItemId == Shop).Name);
        Assert.Equal("Terminal 2", locations.Single(location => location.ItemId == newId).Address);
        Assert.Equal("Van 1", locations.Single(location => location.ItemId == "van-1").Name);
        Assert.StartsWith("Nothing was saved: the location 'Van 1' belongs to a Asset", owned, StringComparison.Ordinal);
        Assert.StartsWith("Nothing was saved: give the location a name", nameless, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeleteStockLocation_RemovesALocation_ButNeverTheDefaultOne()
    {
        // Act
        var removed = AgentToolTestContext.ReadJson(await InvokeAndSaveAsync(DeleteStockLocationTool.TheName, new() { ["locationId"] = Shop }));
        var defaultLocation = await InvokeAndSaveAsync(DeleteStockLocationTool.TheName, new() { ["locationId"] = InventoryConstants.DefaultLocationId });
        var unknown = await InvokeAndSaveAsync(DeleteStockLocationTool.TheName, new() { ["locationId"] = "attic" });

        // Assert
        Assert.True(removed.GetProperty("removed").GetBoolean());
        Assert.DoesNotContain(await LocationsAsync(), location => location.ItemId == Shop);
        Assert.StartsWith("Nothing was removed: the default location cannot be removed", defaultLocation, StringComparison.Ordinal);
        Assert.StartsWith("Nothing was removed: there is no stock location with the id 'attic'", unknown, StringComparison.Ordinal);
    }

    // The location tools write in the chat message's unit of work, which is committed when the message ends.
    private async Task<string> InvokeAndSaveAsync(string toolName, Dictionary<string, object> arguments)
    {
        var services = Build();
        var result = await InvokeAsync(toolName, services, arguments);

        await services.GetRequiredService<ISession>().SaveChangesAsync(TestContext.Current.CancellationToken);

        return result;
    }

    private static JsonElement Rows(string json)
        => JsonSerializer.Deserialize<JsonElement>(json);

    private static IClock Clock()
    {
        var clock = new Mock<IClock>();
        clock.Setup(value => value.UtcNow).Returns(_now);

        return clock.Object;
    }

    private static Dictionary<string, object> Arguments(string toolName)
        => toolName switch
        {
            GetStockItemTool.TheName => new() { ["stockItemId"] = "mug" },
            AdjustStockTool.TheName => new() { ["stockItemId"] = "mug", ["kind"] = "receive", ["quantity"] = 5, ["reason"] = "Delivery" },
            TransferStockTool.TheName => new() { ["stockItemId"] = "mug", ["fromLocationId"] = "default", ["toLocationId"] = Shop, ["quantity"] = 1 },
            ImportStockCountsTool.TheName => new() { ["rows"] = Rows("""[{ "sku": "MUG-BLUE", "count": 1 }]"""), ["reason"] = "Count" },
            SaveStockLocationTool.TheName => new() { ["name"] = "Kiosk" },
            DeleteStockLocationTool.TheName => new() { ["locationId"] = Shop },
            _ => [],
        };

    private async Task<StockItem> FindAsync(string stockItemId)
    {
        await using var session = _store.CreateSession();

        return await new StockItemStore(session).FindByIdAsync(stockItemId, TestContext.Current.CancellationToken);
    }

    private async Task<IReadOnlyCollection<StockLocation>> LocationsAsync()
    {
        await using var session = _store.CreateSession();

        return await new StockLocationStore(session).GetAllAsync(TestContext.Current.CancellationToken);
    }

    private ServiceProvider Build(bool signedIn = true, Func<Permission, object, bool> authorize = null)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);

        services.AddSingleton(Clock());
        services.AddSingleton<ISession>(_ => _store.CreateSession());
        services.AddSingleton<IStockItemStore>(provider => new StockItemStore(provider.GetRequiredService<ISession>()));
        services.AddSingleton<IStockMovementStore>(provider => new StockMovementStore(provider.GetRequiredService<ISession>()));
        services.AddSingleton(provider => new StockLocationStore(provider.GetRequiredService<ISession>()));
        services.AddSingleton<IInventoryService>(provider => new DefaultInventoryService(
            provider.GetRequiredService<ISession>(),
            provider.GetRequiredService<IStockItemStore>(),
            provider.GetRequiredService<IStockMovementStore>(),
            provider.GetRequiredService<IClock>()));
        services.AddSingleton<IInventoryLocationService>(provider => new InventoryLocationService(provider.GetRequiredService<ISiteService>(), provider.GetRequiredService<StockLocationStore>()));
        services.AddSingleton(provider => new StockCountImporter(provider.GetRequiredService<IStockItemStore>(), provider.GetRequiredService<IInventoryService>()));
        services.AddSingleton<InventoryToolScope>();

        var settings = _settings;
        var site = new Mock<ISite>();
        site.Setup(value => value.Properties).Returns(new System.Text.Json.Nodes.JsonObject());
        site.Setup(value => value.GetOrCreate<InventorySettings>()).Returns(_settings);
        site.Setup(value => value.TryGet(out settings)).Returns(true);
        var siteService = new Mock<ISiteService>();
        siteService.Setup(value => value.GetSiteSettingsAsync()).ReturnsAsync(site.Object);
        siteService.Setup(value => value.LoadSiteSettingsAsync()).ReturnsAsync(site.Object);
        services.AddSingleton(siteService.Object);

        return services.BuildServiceProvider();
    }

    private static Task<string> InvokeAsync(string toolName, IServiceProvider services, Dictionary<string, object> arguments)
    {
        AIFunction tool = toolName switch
        {
            ListStockItemsTool.TheName => new ListStockItemsTool(),
            GetStockItemTool.TheName => new GetStockItemTool(),
            AdjustStockTool.TheName => new AdjustStockTool(),
            TransferStockTool.TheName => new TransferStockTool(),
            ImportStockCountsTool.TheName => new ImportStockCountsTool(),
            SyncInventoryTool.TheName => new SyncInventoryTool(),
            ListStockLocationsTool.TheName => new ListStockLocationsTool(),
            SaveStockLocationTool.TheName => new SaveStockLocationTool(),
            _ => new DeleteStockLocationTool(),
        };

        return AgentToolTestContext.InvokeAsync(tool, services, arguments);
    }
}
