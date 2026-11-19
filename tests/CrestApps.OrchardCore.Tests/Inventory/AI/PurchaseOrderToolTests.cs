using CrestApps.OrchardCore.Inventory;
using CrestApps.OrchardCore.Inventory.AI;
using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Inventory.Core.Indexes;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.Inventory.Migrations;
using CrestApps.OrchardCore.Inventory.Services;
using CrestApps.OrchardCore.Numbering;
using CrestApps.OrchardCore.Products.Core.Services;
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
/// The purchase order tools check the Purchase orders page's permissions first, number a new purchase order from the
/// locked sequence, start it with the items low on stock, change a draft's lines and only an ordered one's costs, receive
/// stock at the order's location as movements with the caller as actor, and cancel and delete only what the page allows.
/// </summary>
public sealed class PurchaseOrderToolTests : IAsyncLifetime
{
    private static readonly DateTime _now = new(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"crestapps-purchase-order-tools-{Guid.NewGuid():N}.db");
    private readonly InventorySettings _settings = new();
    private long _sequence;
    private IStore _store;

    public static TheoryData<string, string> ToolsAndPermissions => new()
    {
        { ListPurchaseOrdersTool.TheName, InventoryPermissions.ViewInventory.Name },
        { GetPurchaseOrderTool.TheName, InventoryPermissions.ViewInventory.Name },
        { SavePurchaseOrderTool.TheName, InventoryPermissions.ManageInventory.Name },
        { MarkPurchaseOrderOrderedTool.TheName, InventoryPermissions.ManageInventory.Name },
        { ReceivePurchaseOrderTool.TheName, InventoryPermissions.ManageInventory.Name },
        { CancelPurchaseOrderTool.TheName, InventoryPermissions.ManageInventory.Name },
        { DeletePurchaseOrderTool.TheName, InventoryPermissions.ManageInventory.Name },
    };

    public async ValueTask InitializeAsync()
    {
        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_path};Pooling=False"));
        _store.RegisterIndexes([new StockItemIndexProvider(), new StockMovementIndexProvider(), new StockReservationIndexProvider(), new StockLocationIndexProvider(), new PurchaseOrderIndexProvider()], InventoryConstants.CollectionName);
        await _store.InitializeAsync(TestContext.Current.CancellationToken);
        await _store.InitializeCollectionAsync(InventoryConstants.CollectionName, TestContext.Current.CancellationToken);

        await using (var session = _store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
            var schemaBuilder = new SchemaBuilder(_store.Configuration, transaction);
            await new InventoryMigrations(Mock.Of<IContentDefinitionManager>()) { SchemaBuilder = schemaBuilder }.CreateAsync();
            await new PurchaseOrderMigrations { SchemaBuilder = schemaBuilder }.CreateAsync();
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        await using (var session = _store.CreateSession())
        {
            await new StockLocationStore(session).CreateAsync(new StockLocation { ItemId = InventoryConstants.DefaultLocationId, Name = "Warehouse", Position = 1 }, TestContext.Current.CancellationToken);

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
        // Arrange
        var orderId = await CreateDraftAsync();

        // Act
        var result = await InvokeAsync(toolName, Build(signedIn: false), Arguments(toolName, orderId));

        // Assert
        Assert.Equal(InventoryAIToolBase.NotAllowedMessage, result);
        Assert.NotNull(permission);
        Assert.Equal(PurchaseOrderStatus.Draft, (await FindAsync(orderId)).Status);
    }

    [Theory]
    [MemberData(nameof(ToolsAndPermissions))]
    public async Task Tool_RefusesACallerWithoutThePermission(string toolName, string permission)
    {
        // Arrange
        var orderId = await CreateDraftAsync();
        var services = Build(authorize: (asked, _) => asked.Name != permission);

        // Act
        var result = await InvokeAsync(toolName, services, Arguments(toolName, orderId));

        // Assert
        Assert.Equal(InventoryAIToolBase.NotAllowedMessage, result);
        Assert.Equal(permission, Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks).Permission);
        Assert.Equal(PurchaseOrderStatus.Draft, (await FindAsync(orderId)).Status);
    }

    [Fact]
    public async Task SavePurchaseOrder_StartsADraftWithTheLowStockItems_NumberedFromTheSequence()
    {
        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(SavePurchaseOrderTool.TheName, Build(), new()
        {
            ["supplier"] = "Acme Supplies",
            ["startWithLowStock"] = true,
            ["expectedDate"] = "2026-12-01",
        }));

        // Assert
        var order = json.GetProperty("purchaseOrder");
        var line = Assert.Single(order.GetProperty("lines").EnumerateArray());
        var stored = await FindAsync(order.GetProperty("purchaseOrderId").GetString());

        Assert.StartsWith("PO-261118-", order.GetProperty("number").GetString(), StringComparison.Ordinal);
        Assert.Equal("draft", order.GetProperty("status").GetString());
        Assert.Equal("USD", order.GetProperty("currency").GetString());
        Assert.Equal("2026-12-01", order.GetProperty("expectedDate").GetString());
        Assert.Equal("candle", line.GetProperty("stockItemId").GetString());
        Assert.Equal(3, line.GetProperty("ordered").GetInt32());
        Assert.Equal("admin", stored.Author);
        Assert.Equal(1L, _sequence);
    }

    [Fact]
    public async Task SavePurchaseOrder_AddsAndChangesADraftsLines_AndRefusesWhatThePageWouldIgnore()
    {
        // Arrange
        var orderId = await CreateDraftAsync();

        // Act
        var saved = AgentToolTestContext.ReadJson(await InvokeAsync(SavePurchaseOrderTool.TheName, Build(), new()
        {
            ["purchaseOrderId"] = orderId,
            ["lines"] = Rows("""[{ "stockItemId": "mug", "quantity": 10, "unitCost": 2.5 }]"""),
        }));
        var unknownItem = await InvokeAsync(SavePurchaseOrderTool.TheName, Build(), new() { ["purchaseOrderId"] = orderId, ["lines"] = Rows("""[{ "stockItemId": "nope" }]""") });
        var noSupplier = await InvokeAsync(SavePurchaseOrderTool.TheName, Build(), new() { ["note"] = "No supplier" });
        var unknownLocation = await InvokeAsync(SavePurchaseOrderTool.TheName, Build(), new() { ["supplier"] = "Acme", ["locationId"] = "nowhere" });

        // Assert
        var line = Assert.Single(saved.GetProperty("purchaseOrder").GetProperty("lines").EnumerateArray());
        var stored = await FindAsync(orderId);

        Assert.Equal(10, line.GetProperty("ordered").GetInt32());
        Assert.Equal(25m, line.GetProperty("lineTotal").GetDecimal());
        Assert.Equal(25m, stored.Total);
        Assert.Contains("no stock item has the id 'nope'", unknownItem, StringComparison.Ordinal);
        Assert.Contains("say who the stock is ordered from", noSupplier, StringComparison.Ordinal);
        Assert.Contains("no stock location with the id 'nowhere'", unknownLocation, StringComparison.Ordinal);
        Assert.Single((await ListAsync()).Where(order => order.Supplier == "Acme Supplies"));
    }

    [Fact]
    public async Task MarkOrderedAndReceive_AddStockAtTheOrdersLocation_AndCompleteTheOrder()
    {
        // Arrange
        var orderId = await CreateDraftAsync(("mug", 10));

        // Act
        var draftReceive = await InvokeAsync(ReceivePurchaseOrderTool.TheName, Build(), new() { ["purchaseOrderId"] = orderId, ["all"] = true });
        var ordered = AgentToolTestContext.ReadJson(await InvokeAsync(MarkPurchaseOrderOrderedTool.TheName, Build(), new() { ["purchaseOrderId"] = orderId }));
        var lineId = (await FindAsync(orderId)).Lines[0].LineId;
        var partly = AgentToolTestContext.ReadJson(await InvokeAsync(ReceivePurchaseOrderTool.TheName, Build(), new() { ["purchaseOrderId"] = orderId, ["lines"] = Rows($$"""[{ "lineId": "{{lineId}}", "quantity": 4 }]""") }));
        var rest = AgentToolTestContext.ReadJson(await InvokeAsync(ReceivePurchaseOrderTool.TheName, Build(), new() { ["purchaseOrderId"] = orderId, ["all"] = true }));

        // Assert
        Assert.Contains("once purchase order", draftReceive, StringComparison.Ordinal);
        Assert.Equal("ordered", ordered.GetProperty("purchaseOrder").GetProperty("status").GetString());
        Assert.Equal("partiallyReceived", partly.GetProperty("purchaseOrder").GetProperty("status").GetString());
        Assert.Equal(6, rest.GetProperty("received").GetInt32());
        Assert.Equal("received", rest.GetProperty("purchaseOrder").GetProperty("status").GetString());
        Assert.Equal(30, (await FindItemAsync("mug")).OnHand);
        Assert.Equal(PurchaseOrderStatus.Received, (await FindAsync(orderId)).Status);
    }

    [Fact]
    public async Task OrderedPurchaseOrder_KeepsItsQuantities_AndCanBeCanceledNotDeleted()
    {
        // Arrange
        var orderId = await CreateDraftAsync(("mug", 10));
        await InvokeAsync(MarkPurchaseOrderOrderedTool.TheName, Build(), new() { ["purchaseOrderId"] = orderId });
        var lineId = (await FindAsync(orderId)).Lines[0].LineId;

        // Act
        var quantity = await InvokeAsync(SavePurchaseOrderTool.TheName, Build(), new() { ["purchaseOrderId"] = orderId, ["lines"] = Rows($$"""[{ "lineId": "{{lineId}}", "quantity": 99 }]""") });
        var cost = AgentToolTestContext.ReadJson(await InvokeAsync(SavePurchaseOrderTool.TheName, Build(), new() { ["purchaseOrderId"] = orderId, ["lines"] = Rows($$"""[{ "lineId": "{{lineId}}", "unitCost": 3 }]""") }));
        var deleted = await InvokeAsync(DeletePurchaseOrderTool.TheName, Build(), new() { ["purchaseOrderId"] = orderId });
        var canceled = AgentToolTestContext.ReadJson(await InvokeAsync(CancelPurchaseOrderTool.TheName, Build(), new() { ["purchaseOrderId"] = orderId }));
        var again = await InvokeAsync(CancelPurchaseOrderTool.TheName, Build(), new() { ["purchaseOrderId"] = orderId });

        // Assert
        var stored = await FindAsync(orderId);

        Assert.Contains("only its lines' unit costs can change", quantity, StringComparison.Ordinal);
        Assert.Equal(30m, cost.GetProperty("purchaseOrder").GetProperty("total").GetDecimal());
        Assert.Contains("Only a draft can be deleted", deleted, StringComparison.Ordinal);
        Assert.Equal("canceled", canceled.GetProperty("purchaseOrder").GetProperty("status").GetString());
        Assert.Contains("is already canceled", again, StringComparison.Ordinal);
        Assert.Equal(10, stored.Lines[0].Ordered);
        Assert.Equal(PurchaseOrderStatus.Canceled, stored.Status);
    }

    [Fact]
    public async Task DeletePurchaseOrder_DeletesADraft_AndListAndGetFindItByNumber()
    {
        // Arrange
        var orderId = await CreateDraftAsync(("mug", 2));
        var number = (await FindAsync(orderId)).Number;

        // Act
        var listed = AgentToolTestContext.ReadJson(await InvokeAsync(ListPurchaseOrdersTool.TheName, Build(), new() { ["status"] = "draft" }));
        var found = AgentToolTestContext.ReadJson(await InvokeAsync(GetPurchaseOrderTool.TheName, Build(), new() { ["purchaseOrderId"] = number }));
        var deleted = AgentToolTestContext.ReadJson(await InvokeAsync(DeletePurchaseOrderTool.TheName, Build(), new() { ["purchaseOrderId"] = orderId }));
        var missing = await InvokeAsync(GetPurchaseOrderTool.TheName, Build(), new() { ["purchaseOrderId"] = orderId });

        // Assert
        Assert.Equal(1, listed.GetProperty("total").GetInt32());
        Assert.Equal(orderId, found.GetProperty("purchaseOrderId").GetString());
        Assert.Equal(number, deleted.GetProperty("deleted").GetProperty("number").GetString());
        Assert.Contains("no purchase order has the id or number", missing, StringComparison.Ordinal);
        Assert.Null(await FindAsync(orderId));
    }

    private async Task<string> CreateDraftAsync(params (string StockItemId, int Quantity)[] lines)
    {
        var arguments = new Dictionary<string, object> { ["supplier"] = "Acme Supplies" };

        if (lines.Length > 0)
        {
            arguments["lines"] = Rows(System.Text.Json.JsonSerializer.Serialize(lines.Select(line => new { stockItemId = line.StockItemId, quantity = line.Quantity })));
        }

        var json = AgentToolTestContext.ReadJson(await InvokeAsync(SavePurchaseOrderTool.TheName, Build(), arguments));

        return json.GetProperty("purchaseOrder").GetProperty("purchaseOrderId").GetString();
    }

    private static System.Text.Json.JsonElement Rows(string json)
        => System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(json);

    private static IClock Clock()
    {
        var clock = new Mock<IClock>();
        clock.Setup(value => value.UtcNow).Returns(_now);

        return clock.Object;
    }

    private static Dictionary<string, object> Arguments(string toolName, string orderId)
        => toolName switch
        {
            ListPurchaseOrdersTool.TheName => [],
            SavePurchaseOrderTool.TheName => new() { ["purchaseOrderId"] = orderId, ["note"] = "Changed" },
            ReceivePurchaseOrderTool.TheName => new() { ["purchaseOrderId"] = orderId, ["all"] = true },
            _ => new() { ["purchaseOrderId"] = orderId },
        };

    private async Task<PurchaseOrder> FindAsync(string purchaseOrderId)
    {
        await using var session = _store.CreateSession();

        return await new PurchaseOrderStore(session).FindByIdAsync(purchaseOrderId, TestContext.Current.CancellationToken);
    }

    private async Task<IReadOnlyList<PurchaseOrder>> ListAsync()
    {
        await using var session = _store.CreateSession();

        return (await new PurchaseOrderStore(session).PageAsync(new PurchaseOrderQuery { All = true }, 0, 50, TestContext.Current.CancellationToken)).Orders;
    }

    private async Task<StockItem> FindItemAsync(string stockItemId)
    {
        await using var session = _store.CreateSession();

        return await new StockItemStore(session).FindByIdAsync(stockItemId, TestContext.Current.CancellationToken);
    }

    private ServiceProvider Build(bool signedIn = true, Func<Permission, object, bool> authorize = null)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);

        services.AddSingleton(Clock());
        services.AddSingleton<ISession>(_ => _store.CreateSession());
        services.AddSingleton<IStockItemStore>(provider => new StockItemStore(provider.GetRequiredService<ISession>()));
        services.AddSingleton<IStockMovementStore>(provider => new StockMovementStore(provider.GetRequiredService<ISession>()));
        services.AddSingleton<IPurchaseOrderStore>(provider => new PurchaseOrderStore(provider.GetRequiredService<ISession>()));
        services.AddSingleton(provider => new StockLocationStore(provider.GetRequiredService<ISession>()));
        services.AddSingleton<IInventoryService>(provider => new DefaultInventoryService(
            provider.GetRequiredService<ISession>(),
            provider.GetRequiredService<IStockItemStore>(),
            provider.GetRequiredService<IStockMovementStore>(),
            provider.GetRequiredService<IClock>()));
        services.AddSingleton(provider => new PurchaseOrderReceiver(provider.GetRequiredService<IInventoryService>(), provider.GetRequiredService<IPurchaseOrderStore>(), provider.GetRequiredService<IClock>()));
        services.AddSingleton<IInventoryLocationService>(provider => new InventoryLocationService(provider.GetRequiredService<ISiteService>(), provider.GetRequiredService<StockLocationStore>()));
        services.AddSingleton<InventoryToolScope>();

        var numbers = new Mock<INumberSequence>();
        numbers.Setup(value => value.NextAsync(It.IsAny<string>(), It.IsAny<NumberSequenceOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Interlocked.Increment(ref _sequence));
        services.AddSingleton(numbers.Object);

        var currencies = new Mock<IProductCurrencyProvider>();
        currencies.Setup(value => value.GetCurrenciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<CurrencyDefinition>)[new CurrencyDefinition { CurrencyCode = "USD", DisplayName = "US dollar" }]);
        services.AddSingleton(currencies.Object);

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
            ListPurchaseOrdersTool.TheName => new ListPurchaseOrdersTool(),
            GetPurchaseOrderTool.TheName => new GetPurchaseOrderTool(),
            SavePurchaseOrderTool.TheName => new SavePurchaseOrderTool(),
            MarkPurchaseOrderOrderedTool.TheName => new MarkPurchaseOrderOrderedTool(),
            ReceivePurchaseOrderTool.TheName => new ReceivePurchaseOrderTool(),
            CancelPurchaseOrderTool.TheName => new CancelPurchaseOrderTool(),
            _ => new DeletePurchaseOrderTool(),
        };

        return AgentToolTestContext.InvokeAsync(tool, services, arguments);
    }
}
