using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Inventory.Core.Indexes;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.Inventory.Migrations;
using CrestApps.OrchardCore.Inventory.Services;
using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Modules;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Inventory;

/// <summary>
/// Running low, selling out and coming back are told once a change is committed; stock counts go in and out as CSV
/// or a recipe; and listings know what is in stock.
/// </summary>
public sealed class InventoryStockLevelTests : IAsyncLifetime
{
    private static readonly DateTime _now = new(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);
    private static readonly StockActor _admin = new("user-1", "admin");

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"crestapps-stock-levels-{Guid.NewGuid():N}.db");
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

    [Theory]
    [InlineData(10, 5, 5, StockLevel.InStock, StockLevel.Low, true, false, false)]
    [InlineData(10, 0, 5, StockLevel.InStock, StockLevel.Out, true, true, false)]
    [InlineData(3, 0, 5, StockLevel.Low, StockLevel.Out, true, true, false)]
    [InlineData(4, 3, 5, StockLevel.Low, StockLevel.Low, false, false, false)]
    [InlineData(0, 2, 5, StockLevel.Out, StockLevel.Low, false, false, true)]
    [InlineData(-2, 20, 5, StockLevel.Out, StockLevel.InStock, false, false, true)]
    [InlineData(0, -1, 5, StockLevel.Out, StockLevel.Out, false, false, false)]
    public void ALevelChange_SaysWhetherItRanLow_SoldOut_OrCameBack(int before, int after, int threshold, StockLevel previousLevel, StockLevel level, bool ranLow, bool soldOut, bool backInStock)
    {
        var context = new StockLevelChangedContext(new StockItem { OnHand = after, LowStockThreshold = threshold }, before);

        Assert.Equal(previousLevel, context.PreviousLevel);
        Assert.Equal(level, context.Level);
        Assert.Equal(ranLow, context.RanLow);
        Assert.Equal(soldOut, context.SoldOut);
        Assert.Equal(backInStock, context.BackInStock);
    }

    [Fact]
    public async Task EveryChangeToWhatIsAvailable_IsObserved_WithTheUnitsAvailableBeforeIt()
    {
        var itemId = await AddItemAsync(onHand: 10);
        var observer = new RecordingObserver();

        await InSessionAsync(service => service.AdjustAsync(itemId, StockMovementKind.Adjusted, -2, "Damaged", _admin, TestContext.Current.CancellationToken), observer);
        await InSessionAsync(service => service.ReserveAsync("order-a", [new StockRequest("mug", null, 3)], _now.AddHours(1), TestContext.Current.CancellationToken), observer);
        await InSessionAsync(service => service.ReleaseAsync("order-a", "Canceled", TestContext.Current.CancellationToken), observer);
        await InSessionAsync(service => service.SetCountAsync(itemId, 1, "Shelf count", _admin, TestContext.Current.CancellationToken), observer);

        Assert.Equal([(10, 8), (8, 5), (5, 8), (8, 1)], observer.Changes);
    }

    [Fact]
    public async Task ACommittedDropBelowTheThreshold_IsTold_OnceTheWorkIsCommitted()
    {
        var itemId = await AddItemAsync(onHand: 10);
        // Two changes in one unit of work are one: 10 available before, 2 after.
        await InSessionAsync(async service =>
        {
            await service.AdjustAsync(itemId, StockMovementKind.Adjusted, -3, null, _admin, TestContext.Current.CancellationToken);

            return await service.AdjustAsync(itemId, StockMovementKind.Adjusted, -5, null, _admin, TestContext.Current.CancellationToken);
        }, observer: null);

        var handler = new RecordingHandler();
        await DispatchAsync(new Dictionary<string, int> { [itemId] = 10 }, handler);

        var change = Assert.Single(handler.Contexts);
        Assert.Equal(StockLevel.InStock, change.PreviousLevel);
        Assert.Equal(StockLevel.Low, change.Level);
        Assert.Equal(2, change.Item.Available);
        Assert.True(change.RanLow);
        Assert.False(change.SoldOut);
    }

    [Fact]
    public async Task AChangeThatWasRolledBack_TellsNothing()
    {
        var itemId = await AddItemAsync(onHand: 10);

        await using (var session = _store.CreateSession())
        {
            await CreateService(session).AdjustAsync(itemId, StockMovementKind.Adjusted, -10, null, _admin, TestContext.Current.CancellationToken);

            // A checkout that lost a race for the same stock is canceled, never committed.
            await session.CancelAsync();
        }

        var handler = new RecordingHandler();
        await DispatchAsync(new Dictionary<string, int> { [itemId] = 10 }, handler);

        Assert.Empty(handler.Contexts);
    }

    [Fact]
    public async Task OneFailingHandler_DoesNotKeepTheOthersFromHearing()
    {
        var itemId = await AddItemAsync(onHand: 0);
        var handler = new RecordingHandler();

        await DispatchAsync(new Dictionary<string, int> { [itemId] = 4 }, new ThrowingHandler(), handler);

        Assert.True(Assert.Single(handler.Contexts).SoldOut);
    }

    [Fact]
    public async Task AChangeToWhatIsAvailable_IsTold_EvenWhenTheLevelStaysTheSame()
    {
        var itemId = await AddItemAsync(onHand: 20);
        var handler = new RecordingHandler();

        await DispatchAsync(new Dictionary<string, int> { [itemId] = 30 }, handler);

        var change = Assert.Single(handler.AvailableChanges);
        Assert.Equal(30, change.PreviousAvailable);
        Assert.Equal(20, change.Item.Available);
        Assert.Empty(handler.Contexts);
    }

    [Fact]
    public void Csv_RoundTripsCounts_FindingColumnsByName_AndQuotedValues()
    {
        var writer = new StringWriter();
        StockCsv.WriteHeader(writer);
        StockCsv.Write(writer, new StockItem { Sku = "MUG", Title = "Mug, \"large\"", ContentItemId = "mug", OnHand = 7, Reserved = 2, LowStockThreshold = 5 });
        StockCsv.Write(writer, new StockItem { Sku = "CAP-M", Title = "Cap\nM", ContentItemId = "cap", VariantId = "m", OnHand = 3 });

        var rows = StockCsv.Read(new StringReader(writer.ToString()), 100);

        Assert.Collection(rows,
            row =>
            {
                Assert.Equal(("MUG", "mug", null, 7), (row.Sku, row.ContentItemId, row.VariantId, row.OnHand));
                Assert.Equal(2, row.Line);
            },
            row =>
            {
                Assert.Equal(("CAP-M", "cap", "m", 3), (row.Sku, row.ContentItemId, row.VariantId, row.OnHand));
            });

        // Any order and case, and only what is needed: a SKU or ids, and the units on hand.
        var minimal = StockCsv.Read(new StringReader("onhand,sku\r\n12,MUG\r\n\r\nlots,CAP\r\n"), 100);

        Assert.Equal([("MUG", (int?)12), ("CAP", null)], minimal.Select(row => (row.Sku, row.OnHand)));
        Assert.Null(StockCsv.Read(new StringReader("Sku,Title\r\nMUG,Mug\r\n"), 100));
        Assert.True(StockCsv.Read(new StringReader("Sku,OnHand\r\nA,1\r\nB,2\r\nC,3\r\n"), 2)[^1].TooMany);
    }

    [Fact]
    public async Task ImportedCounts_FindItemsByIdsOrSku_AndReportWhatTheyCouldNotApply()
    {
        var mug = await AddItemAsync(onHand: 10);
        var cap = await AddItemAsync("cap", "m", sku: "CAP-M", onHand: 4);
        await AddItemAsync("bag", "a", sku: "BAG", onHand: 1);
        await AddItemAsync("bag", "b", sku: "BAG", onHand: 1);

        StockCountImportResult result;

        await using (var session = _store.CreateSession())
        {
            var service = CreateService(session);
            var importer = new StockCountImporter(new StockItemStore(session), service);

            result = await importer.ImportAsync(
            [
                new StockCountRow { Line = 2, Sku = "mug", OnHand = 12 },
                new StockCountRow { Line = 3, ContentItemId = "cap", VariantId = "m", Sku = "OLD-SKU", OnHand = 4 },
                new StockCountRow { Line = 4, Sku = "NOPE", OnHand = 1 },
                new StockCountRow { Line = 5, Sku = "BAG", OnHand = 1 },
                new StockCountRow { Line = 6, Sku = "MUG", OnHand = 3 },
                new StockCountRow { Line = 7, Sku = "CAP-M", OnHand = -1 },
            ], "Imported from stock.csv.", _admin, TestContext.Current.CancellationToken);

            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal(1, result.Counted);
        Assert.Equal(1, result.Unchanged);

        // An unknown SKU, a SKU two items share, a second count for the mug, and a negative count.
        Assert.Equal(
            [(4, StockCountProblemKind.NotFound), (5, StockCountProblemKind.NotFound), (6, StockCountProblemKind.Duplicate), (7, StockCountProblemKind.InvalidCount)],
            result.Problems.Select(problem => (problem.Line, problem.Kind)));

        await using (var session = _store.CreateSession())
        {
            var items = new StockItemStore(session);
            Assert.Equal(12, (await items.FindByIdAsync(mug, TestContext.Current.CancellationToken)).OnHand);
            Assert.Equal(4, (await items.FindByIdAsync(cap, TestContext.Current.CancellationToken)).OnHand);

            var (_, movements) = await new StockMovementStore(session).PageAsync(mug, 0, 10, TestContext.Current.CancellationToken);
            var counted = Assert.Single(movements, movement => movement.Kind == StockMovementKind.Counted);
            Assert.Equal(2, counted.Quantity);
            Assert.Equal("Imported from stock.csv.", counted.Reason);
        }
    }

    [Fact]
    public async Task SearchRecords_SayAProductIsInStock_WhenAnyVariantIsAvailable_OrItIsNotCounted()
    {
        Assert.Equal("true", await InStockAsync(new ProductAvailability(0, false, 5), new ProductAvailability(2, false, 5)));
        Assert.Equal("false", await InStockAsync(new ProductAvailability(0, false, 5), new ProductAvailability(-1, false, 5)));

        // Backorders can be bought, but aren't in stock.
        Assert.Equal("false", await InStockAsync(new ProductAvailability(0, true, 5)));
        Assert.Equal("true", await InStockAsync());
    }

    private static async Task<string> InStockAsync(params ProductAvailability[] variants)
    {
        var provider = new Mock<IProductAvailabilityProvider>();
        provider.Setup(value => value.GetAvailabilityAsync(It.IsAny<ProductAvailabilityRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(variants.Select((availability, index) => (Key: new ProductStockKey("mug", "v" + index), availability)).ToDictionary(entry => entry.Key, entry => entry.availability));

        var document = new ProductSearchDocument();
        await new InventorySearchDocumentContributor([provider.Object]).ContributeAsync(new ContentItem { ContentItemId = "mug" }, document);

        return Assert.Single(document.Attributes, attribute => attribute.Key == ProductAttributeKeys.InStock).Text;
    }

    private static IClock Clock()
    {
        var clock = new Mock<IClock>();
        clock.Setup(value => value.UtcNow).Returns(_now);

        return clock.Object;
    }

    private static DefaultInventoryService CreateService(ISession session, IStockLevelObserver observer = null)
        => new(session, new StockItemStore(session), new StockMovementStore(session), Clock(), observer is null ? null : [observer]);

    private async Task<T> InSessionAsync<T>(Func<DefaultInventoryService, Task<T>> work, IStockLevelObserver observer)
    {
        await using var session = _store.CreateSession();
        var result = await work(CreateService(session, observer));
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        return result;
    }

    private async Task DispatchAsync(Dictionary<string, int> previous, params IStockLevelHandler[] handlers)
    {
        await using var session = _store.CreateSession();
        await new StockLevelDispatcher(new StockItemStore(session), handlers, NullLogger<StockLevelDispatcher>.Instance).DispatchAsync(previous, TestContext.Current.CancellationToken);
    }

    private async Task<string> AddItemAsync(string contentItemId = "mug", string variantId = null, string sku = "MUG", int onHand = 0, int threshold = 5)
    {
        await using var session = _store.CreateSession();
        var item = new StockItem
        {
            ItemId = contentItemId + "-" + (variantId ?? "item"),
            ContentItemId = contentItemId,
            VariantId = variantId,
            Title = contentItemId,
            Sku = sku,
            OnHand = onHand,
            LowStockThreshold = threshold,
        };

        await new StockItemStore(session).CreateAsync(item, TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        return item.ItemId;
    }

    private sealed class RecordingObserver : IStockLevelObserver
    {
        public List<(int Before, int After)> Changes { get; } = [];

        public void Changed(StockItem item, int previousAvailable)
            => Changes.Add((previousAvailable, item.Available));
    }

    private sealed class RecordingHandler : IStockLevelHandler
    {
        public List<StockLevelChangedContext> Contexts { get; } = [];

        public List<StockLevelChangedContext> AvailableChanges { get; } = [];

        public Task ChangedAsync(StockLevelChangedContext context, CancellationToken cancellationToken = default)
        {
            Contexts.Add(context);

            return Task.CompletedTask;
        }

        public Task AvailableChangedAsync(StockLevelChangedContext context, CancellationToken cancellationToken = default)
        {
            AvailableChanges.Add(context);

            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingHandler : IStockLevelHandler
    {
        public Task ChangedAsync(StockLevelChangedContext context, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The mail server is down.");
    }
}
