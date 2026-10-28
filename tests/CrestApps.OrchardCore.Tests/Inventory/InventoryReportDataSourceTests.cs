using System.Globalization;
using System.Security.Claims;
using CrestApps.OrchardCore.Inventory;
using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Inventory.Core.Indexes;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.Inventory.Migrations;
using CrestApps.OrchardCore.Inventory.Reports.DataSources;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.DataSources;
using OrchardCore.Security;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Inventory;

/// <summary>
/// The inventory data source reads real stock items, movements, holds, purchase orders and back-in-stock alerts from a
/// SQLite store, and the locations from the location service, so the index queries, the fields they expose, the
/// push-down of dates, join keys and kinds, the grouping in the database, and the permission they require are all
/// exercised end to end.
/// </summary>
public sealed class InventoryReportDataSourceTests : IAsyncLifetime
{
    private static readonly ClaimsPrincipal _user = ReportDesignerPrincipals.User("u1", "Warehouse");
    private static readonly DateTime _start = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"inventory-reports-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_databasePath};Pooling=False"));
        _store.RegisterIndexes(
            [
                new StockItemIndexProvider(),
                new StockMovementIndexProvider(),
                new StockReservationIndexProvider(),
                new PurchaseOrderIndexProvider(),
                new StockAlertIndexProvider(),
            ],
            InventoryConstants.CollectionName);

        await _store.InitializeAsync(cancellationToken);
        await _store.InitializeCollectionAsync(InventoryConstants.CollectionName, cancellationToken);

        await using (var session = _store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(cancellationToken);
            var schemaBuilder = new SchemaBuilder(_store.Configuration, transaction);

            await new InventoryMigrations(Mock.Of<IContentDefinitionManager>()) { SchemaBuilder = schemaBuilder }.CreateAsync();
            await new PurchaseOrderMigrations { SchemaBuilder = schemaBuilder }.CreateAsync();
            await new StockAlertMigrations { SchemaBuilder = schemaBuilder }.CreateAsync();

            await transaction.CommitAsync(cancellationToken);
        }

        await using (var session = _store.CreateSession())
        {
            // A mug kept at two locations, a red cap counted before there were locations, and an archived item.
            await SaveAsync(session, new StockItem
            {
                ItemId = "item-mug",
                ContentItemId = "product-mug",
                Sku = "MUG-1",
                Title = "Mug",
                OnHand = 10,
                Reserved = 2,
                LowStockThreshold = 5,
                Levels =
                [
                    new StockLocationLevel { LocationId = InventoryConstants.DefaultLocationId, OnHand = 6, Reserved = 2 },
                    new StockLocationLevel { LocationId = "shop", OnHand = 4 },
                ],
                CreatedUtc = _start,
                ModifiedUtc = _start.AddDays(3),
            });

            await SaveAsync(session, new StockItem
            {
                ItemId = "item-cap-red",
                ContentItemId = "product-cap",
                VariantId = "red",
                Sku = "CAP-R",
                Title = "Cap, Red",
                OnHand = 3,
                LowStockThreshold = 5,
                AllowBackorder = true,
                CreatedUtc = _start,
                ModifiedUtc = _start.AddDays(2),
            });

            await SaveAsync(session, new StockItem
            {
                ItemId = "item-old",
                ContentItemId = "product-old",
                Sku = "OLD-1",
                Title = "Old",
                LowStockThreshold = 5,
                Archived = true,
                CreatedUtc = _start,
                ModifiedUtc = _start,
            });

            // One movement a day.
            await SaveAsync(session, Movement("m-0", "item-mug", StockMovementKind.Received, 10, 0, InventoryConstants.DefaultLocationId, purchaseOrderId: "po-1"));
            await SaveAsync(session, Movement("m-1", "item-mug", StockMovementKind.Sold, -2, 1, InventoryConstants.DefaultLocationId, orderId: "order-1"));
            await SaveAsync(session, Movement("m-2", "item-cap-red", StockMovementKind.Adjusted, 3, 2, null, reason: "Found"));
            await SaveAsync(session, Movement("m-3", "item-mug", StockMovementKind.Transferred, 4, 3, "shop"));

            await SaveAsync(session, new StockReservation
            {
                ItemId = "r-1",
                StockItemId = "item-mug",
                OrderId = "order-1",
                Quantity = 2,
                Status = StockReservationStatus.Held,
                ExpiresUtc = _start.AddHours(1),
                CreatedUtc = _start,
                ModifiedUtc = _start,
            });

            await SaveAsync(session, new StockReservation
            {
                ItemId = "r-2",
                StockItemId = "item-cap-red",
                OrderId = "order-2",
                Quantity = 1,
                LocationId = "shop",
                Status = StockReservationStatus.Committed,
                ExpiresUtc = _start.AddDays(1).AddHours(1),
                CreatedUtc = _start.AddDays(1),
                ModifiedUtc = _start.AddDays(1),
            });

            await SaveAsync(session, new PurchaseOrder
            {
                ItemId = "po-1",
                Number = "PO-1",
                Supplier = "Acme",
                SupplierEmail = "orders@acme.test",
                Currency = "USD",
                Status = PurchaseOrderStatus.PartiallyReceived,
                Note = "Secret terms",
                Lines =
                [
                    new PurchaseOrderLine { LineId = "l-1", StockItemId = "item-mug", Title = "Mug", Sku = "MUG-1", Ordered = 10, Received = 10, UnitCost = 2.5m },
                    new PurchaseOrderLine { LineId = "l-2", StockItemId = "item-cap-red", Title = "Cap, Red", Sku = "CAP-R", Ordered = 5, UnitCost = 4m },
                ],
                CreatedUtc = _start,
                ModifiedUtc = _start,
                OrderedUtc = _start.AddHours(1),
                OwnerId = "u1",
                Author = "Ada",
            });

            await SaveAsync(session, new PurchaseOrder
            {
                ItemId = "po-2",
                Number = "PO-2",
                Supplier = "Globex",
                LocationId = "shop",
                Currency = "EUR",
                Status = PurchaseOrderStatus.Draft,
                CreatedUtc = _start.AddDays(1),
                ModifiedUtc = _start.AddDays(1),
            });

            await SaveAsync(session, new StockAlert
            {
                StockItemId = "item-cap-red",
                ContentItemId = "product-cap",
                VariantId = "red",
                Email = "shopper@example.com",
                UserId = "user-9",
                CreatedUtc = _start,
            });

            await SaveAsync(session, new StockAlert
            {
                StockItemId = "item-mug",
                ContentItemId = "product-mug",
                Email = "guest@example.com",
                CreatedUtc = _start.AddDays(1),
                SentUtc = _start.AddDays(2),
            });

            await session.SaveChangesAsync(cancellationToken);
        }
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task WithoutViewInventory_NothingIsExposed()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session, grants: []);
        var context = new DataSourceContext { User = _user };

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.InventorySource.StockItems, context, TestContext.Current.CancellationToken);
        var items = await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.InventorySource.StockItems), TestContext.Current.CancellationToken);
        var movements = await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.InventorySource.StockMovements), TestContext.Current.CancellationToken);
        var locations = await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.InventorySource.Locations), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(dataSets);
        Assert.Null(schema);
        Assert.Empty(items.Rows);
        Assert.Empty(movements.Rows);
        Assert.Empty(locations.Rows);
    }

    [Fact]
    public async Task WithoutAUser_NothingIsExposed()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var context = new DataSourceContext();

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.InventorySource.Locations, context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(dataSets);
        Assert.Null(schema);
    }

    [Fact]
    public async Task Schema_DeclaresDatesKeysAndReferences()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var context = new DataSourceContext { User = _user };

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var items = await source.GetSchemaAsync(CommerceReportDataSets.InventorySource.StockItems, context, TestContext.Current.CancellationToken);
        var movements = await source.GetSchemaAsync(CommerceReportDataSets.InventorySource.StockMovements, context, TestContext.Current.CancellationToken);
        var reservations = await source.GetSchemaAsync(CommerceReportDataSets.InventorySource.StockReservations, context, TestContext.Current.CancellationToken);
        var locations = await source.GetSchemaAsync(CommerceReportDataSets.InventorySource.Locations, context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [
                CommerceReportDataSets.InventorySource.StockItems,
                CommerceReportDataSets.InventorySource.StockLevels,
                CommerceReportDataSets.InventorySource.StockMovements,
                CommerceReportDataSets.InventorySource.StockReservations,
                CommerceReportDataSets.InventorySource.Locations,
                CommerceReportDataSets.InventorySource.PurchaseOrders,
                CommerceReportDataSets.InventorySource.PurchaseOrderLines,
                CommerceReportDataSets.InventorySource.StockAlerts,
            ],
            dataSets.Select(dataSet => dataSet.Name));

        // Stock is a snapshot, and the holds' index has no date they were made, so neither has a main date.
        Assert.Null(items.DataSet.DefaultDateField);
        Assert.Null(reservations.DataSet.DefaultDateField);
        Assert.Equal(StockMovementsReportDataSet.UtcField, movements.DataSet.DefaultDateField);

        Assert.True(items.FindField(CommerceReportDataSets.ItemIdField).IsIdentifier);
        Assert.True(items.FindField(CommerceReportDataSets.ItemIdField).IsKeyFilterable);
        Assert.True(items.FindField(StockItemsReportDataSet.ProductIdField).IsKeyFilterable);
        var product = Assert.Single(items.FindField(StockItemsReportDataSet.ProductIdField).References);
        Assert.Equal(CommerceReportDataSets.ProductSource.Name, product.Source);
        Assert.Equal(CommerceReportDataSets.ContentItemIdField, product.Field);
        var variant = Assert.Single(items.FindField("VariantId").References);
        Assert.Equal(CommerceReportDataSets.VariantIdField, variant.Field);

        var stockItem = Assert.Single(movements.FindField("StockItemId").References);
        Assert.Equal(CommerceReportDataSets.InventorySource.Name, stockItem.Source);
        Assert.Equal(CommerceReportDataSets.InventorySource.StockItems, stockItem.DataSet);
        Assert.Equal(CommerceReportDataSets.ItemIdField, stockItem.Field);
        Assert.True(movements.FindField("StockItemId").IsKeyFilterable);

        var location = Assert.Single(movements.FindField("LocationId").References);
        Assert.Equal(CommerceReportDataSets.InventorySource.Locations, location.DataSet);
        Assert.Equal(CommerceReportDataSets.ItemIdField, location.Field);

        var order = Assert.Single(movements.FindField("OrderId").References);
        Assert.Equal(CommerceReportDataSets.OrderSource.Name, order.Source);
        Assert.Equal(CommerceReportDataSets.OrderSource.Orders, order.DataSet);

        var actor = Assert.Single(movements.FindField("ActorId").References);
        Assert.Equal(ReportsConstants.UsersDataSource, actor.Source);

        // The orders data source joins its orders and returns to the locations by this field.
        Assert.True(locations.FindField(CommerceReportDataSets.ItemIdField).IsIdentifier);
        Assert.True(locations.FindField(CommerceReportDataSets.ItemIdField).IsKeyFilterable);
    }

    [Fact]
    public async Task Schema_NeverExposesNotesOrWhoAskedForAnAlert()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var context = new DataSourceContext { User = _user };

        // Act
        var orders = await source.GetSchemaAsync(CommerceReportDataSets.InventorySource.PurchaseOrders, context, TestContext.Current.CancellationToken);
        var alerts = await source.GetSchemaAsync(CommerceReportDataSets.InventorySource.StockAlerts, context, TestContext.Current.CancellationToken);
        var orderRows = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.InventorySource.PurchaseOrders), TestContext.Current.CancellationToken));
        var alertRows = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.InventorySource.StockAlerts), TestContext.Current.CancellationToken));

        // Assert
        Assert.DoesNotContain(orders.Fields, field => field.Name is "Note");
        Assert.DoesNotContain(orderRows.SelectMany(row => row.Values), value => value is string text && text.Contains("Secret", StringComparison.Ordinal));
        Assert.DoesNotContain(alerts.Fields, field => field.Name is "Email" or "NormalizedEmail" or "UserId");
        Assert.DoesNotContain(alertRows.SelectMany(row => row.Values), value => value is string text && (text.Contains('@') || text == "user-9"));
    }

    [Fact]
    public async Task Query_StockItems_ReturnsTypedValues_NewestFirst()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);

        // Act
        var rows = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.InventorySource.StockItems), TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["item-old", "item-cap-red", "item-mug"], rows.Select(row => row[CommerceReportDataSets.ItemIdField]));

        var mug = rows[2];
        Assert.Equal("product-mug", mug[StockItemsReportDataSet.ProductIdField]);
        Assert.Null(mug["VariantId"]);
        Assert.Equal("MUG-1", mug["Sku"]);
        Assert.Equal(10L, mug["OnHand"]);
        Assert.Equal(2L, mug["Reserved"]);
        Assert.Equal(8L, mug["Available"]);
        Assert.Equal(5L, mug["LowStockThreshold"]);
        Assert.Equal("InStock", mug["StockStatus"]);
        Assert.Equal(false, mug["IsLow"]);
        Assert.Equal(DateTimeKind.Utc, Assert.IsType<DateTime>(mug["ModifiedUtc"]).Kind);

        var cap = rows[1];
        Assert.Equal("red", cap["VariantId"]);
        Assert.Equal("Low", cap["StockStatus"]);
        Assert.Equal(true, cap["IsLow"]);
        Assert.Equal(true, cap["AllowBackorder"]);
        Assert.Equal(false, cap["IsBackordered"]);

        var old = rows[0];
        Assert.Equal(true, old["Archived"]);
        Assert.Equal("Out", old["StockStatus"]);
    }

    [Fact]
    public async Task Query_StockLevels_GivesOneRowPerLocation_AndPutsUnplacedUnitsAtTheDefault()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var mug = Query(CommerceReportDataSets.InventorySource.StockLevels);
        mug.Conditions.Add(new DataCondition { Field = StockLevelsReportDataSet.StockItemIdField, Operator = DataFilterOperator.In, Values = ["item-mug"], IsJoinKey = true });
        var cap = Query(CommerceReportDataSets.InventorySource.StockLevels);
        cap.Conditions.Add(new DataCondition { Field = StockItemsReportDataSet.ProductIdField, Operator = DataFilterOperator.In, Values = ["product-cap"], IsJoinKey = true });

        // Act
        var mugRows = Rows(await ReportDesignerTestServices.ReadAsync(source, mug, TestContext.Current.CancellationToken));
        var capRows = Rows(await ReportDesignerTestServices.ReadAsync(source, cap, TestContext.Current.CancellationToken));
        var all = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.InventorySource.StockLevels), TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal([InventoryConstants.DefaultLocationId, "shop"], mugRows.Select(row => row["LocationId"]));
        Assert.Equal([6L, 4L], mugRows.Select(row => row["OnHand"]));
        Assert.Equal([2L, 0L], mugRows.Select(row => row["Reserved"]));
        Assert.Equal([4L, 4L], mugRows.Select(row => row["Available"]));
        Assert.All(mugRows, row => Assert.Equal("MUG-1", row["Sku"]));

        var level = Assert.Single(capRows);
        Assert.Equal("item-cap-red", level[StockLevelsReportDataSet.StockItemIdField]);
        Assert.Equal(InventoryConstants.DefaultLocationId, level["LocationId"]);
        Assert.Equal(3L, level["OnHand"]);

        // The archived item holds no units, so it has no level.
        Assert.Equal(3, all.Count);
    }

    [Fact]
    public async Task Query_StockMovements_ReturnsTypedValues_NewestFirst()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);

        // Act
        var rows = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.InventorySource.StockMovements), TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["m-3", "m-2", "m-1", "m-0"], rows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal("Transferred", rows[0]["Kind"]);
        Assert.Equal("shop", rows[0]["LocationId"]);
        Assert.Equal(InventoryConstants.DefaultLocationId, rows[1]["LocationId"]);
        Assert.Equal("Found", rows[1]["Reason"]);
        Assert.Equal("Sold", rows[2]["Kind"]);
        Assert.Equal(-2L, rows[2]["Quantity"]);
        Assert.Equal("order-1", rows[2]["OrderId"]);
        Assert.Equal("po-1", rows[3]["PurchaseOrderId"]);
        Assert.Equal("u1", rows[3]["ActorId"]);
        Assert.Equal("Ada", rows[3]["ActorName"]);
        Assert.Equal(_start, rows[3][StockMovementsReportDataSet.UtcField]);
    }

    [Fact]
    public async Task Query_StockMovements_PushDownNeverDropsAMatchingRow()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var dates = Query(CommerceReportDataSets.InventorySource.StockMovements, CommerceReportDataSets.ItemIdField);
        dates.Conditions.Add(Condition(StockMovementsReportDataSet.UtcField, DataFilterOperator.Between, _start.AddDays(1), _start.AddDays(2)));
        var keys = Query(CommerceReportDataSets.InventorySource.StockMovements, CommerceReportDataSets.ItemIdField);
        keys.Conditions.Add(new DataCondition { Field = "StockItemId", Operator = DataFilterOperator.In, Values = ["item-mug"], IsJoinKey = true });
        var kind = Query(CommerceReportDataSets.InventorySource.StockMovements, CommerceReportDataSets.ItemIdField);
        kind.Conditions.Add(Condition("Kind", DataFilterOperator.In, "sold", "received"));
        var none = Query(CommerceReportDataSets.InventorySource.StockMovements, CommerceReportDataSets.ItemIdField);
        none.Conditions.Add(new DataCondition { Field = "OrderId", Operator = DataFilterOperator.In, Values = [], IsJoinKey = true });

        // Act
        var dateRows = Rows(await ReportDesignerTestServices.ReadAsync(source, dates, TestContext.Current.CancellationToken));
        var keyRows = Rows(await ReportDesignerTestServices.ReadAsync(source, keys, TestContext.Current.CancellationToken));
        var kindRows = Rows(await ReportDesignerTestServices.ReadAsync(source, kind, TestContext.Current.CancellationToken));
        var noRows = Rows(await ReportDesignerTestServices.ReadAsync(source, none, TestContext.Current.CancellationToken));

        // Assert
        // The bounds are inclusive, so the rows on them stay; the report applies the strict comparison itself.
        Assert.Equal(["m-2", "m-1"], dateRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal(["m-3", "m-1", "m-0"], keyRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal(["m-1", "m-0"], kindRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Empty(noRows);
    }

    [Fact]
    public async Task Aggregate_StockMovementsAndItems_GroupInTheDatabase()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var byKind = new DataAggregateQuery
        {
            DataSet = CommerceReportDataSets.InventorySource.StockMovements,
            Groups = [new DataAggregateGroup { Field = "Kind" }],
            Measures = [new DataAggregateMeasure { Function = DataAggregate.Count }],
            Context = new DataSourceContext { User = _user },
        };

        var byProduct = new DataAggregateQuery
        {
            DataSet = CommerceReportDataSets.InventorySource.StockItems,
            Groups = [new DataAggregateGroup { Field = StockItemsReportDataSet.ProductIdField }],
            Measures = [new DataAggregateMeasure { Function = DataAggregate.Count }],
            Context = new DataSourceContext { User = _user },
        };

        var levels = new DataAggregateQuery
        {
            DataSet = CommerceReportDataSets.InventorySource.StockLevels,
            Groups = [new DataAggregateGroup { Field = "LocationId" }],
            Measures = [new DataAggregateMeasure { Function = DataAggregate.Count }],
            Context = new DataSourceContext { User = _user },
        };

        // Act
        var kinds = await ((IDataAggregateSource)source).AggregateAsync(byKind, TestContext.Current.CancellationToken);
        var products = await ((IDataAggregateSource)source).AggregateAsync(byProduct, TestContext.Current.CancellationToken);
        var levelGroups = await ((IDataAggregateSource)source).AggregateAsync(levels, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(kinds);
        var kindCounts = kinds.ToDictionary(row => (string)row[0], row => (long)row[1]);
        Assert.Equal(1L, kindCounts["Received"]);
        Assert.Equal(1L, kindCounts["Sold"]);
        Assert.Equal(1L, kindCounts["Adjusted"]);
        Assert.Equal(1L, kindCounts["Transferred"]);

        Assert.NotNull(products);
        var productCounts = products.ToDictionary(row => (string)row[0], row => (long)row[1]);
        Assert.Equal(1L, productCounts["product-mug"]);
        Assert.Equal(1L, productCounts["product-cap"]);
        Assert.Equal(1L, productCounts["product-old"]);

        // The levels are entries of the items, so the index can't group them.
        Assert.Null(levelGroups);
    }

    [Fact]
    public async Task Query_StockReservations_ReadNewestFirst_AndPushDownTheirStatus()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var committed = Query(CommerceReportDataSets.InventorySource.StockReservations, CommerceReportDataSets.ItemIdField);
        committed.Conditions.Add(Condition("Status", DataFilterOperator.In, "committed"));
        var forOrder = Query(CommerceReportDataSets.InventorySource.StockReservations, CommerceReportDataSets.ItemIdField);
        forOrder.Conditions.Add(new DataCondition { Field = "OrderId", Operator = DataFilterOperator.In, Values = ["order-1"], IsJoinKey = true });

        // Act
        var rows = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.InventorySource.StockReservations), TestContext.Current.CancellationToken));
        var committedRows = Rows(await ReportDesignerTestServices.ReadAsync(source, committed, TestContext.Current.CancellationToken));
        var orderRows = Rows(await ReportDesignerTestServices.ReadAsync(source, forOrder, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["r-2", "r-1"], rows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal("Held", rows[1]["Status"]);
        Assert.Equal(2L, rows[1]["Quantity"]);
        Assert.Equal(InventoryConstants.DefaultLocationId, rows[1]["LocationId"]);
        Assert.Equal("shop", rows[0]["LocationId"]);
        Assert.Equal(["r-2"], committedRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal(["r-1"], orderRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
    }

    [Fact]
    public async Task Query_PurchaseOrdersAndTheirLines_ReturnTypedValues()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var context = new DataSourceContext { User = _user };
        var lines = Query(CommerceReportDataSets.InventorySource.PurchaseOrderLines);
        lines.Conditions.Add(new DataCondition { Field = PurchaseOrderLinesReportDataSet.PurchaseOrderIdField, Operator = DataFilterOperator.In, Values = ["po-1"], IsJoinKey = true });
        var dates = Query(CommerceReportDataSets.InventorySource.PurchaseOrders, CommerceReportDataSets.ItemIdField);
        dates.Conditions.Add(Condition(PurchaseOrdersReportDataSet.CreatedUtcField, DataFilterOperator.GreaterThanOrEqual, _start.AddDays(1)));
        var drafts = Query(CommerceReportDataSets.InventorySource.PurchaseOrderLines, PurchaseOrderLinesReportDataSet.PurchaseOrderIdField);
        drafts.Conditions.Add(Condition("PurchaseOrderStatus", DataFilterOperator.In, "draft"));

        // Act
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.InventorySource.PurchaseOrderLines, context, TestContext.Current.CancellationToken);
        var orders = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.InventorySource.PurchaseOrders), TestContext.Current.CancellationToken));
        var lineRows = Rows(await ReportDesignerTestServices.ReadAsync(source, lines, TestContext.Current.CancellationToken));
        var dateRows = Rows(await ReportDesignerTestServices.ReadAsync(source, dates, TestContext.Current.CancellationToken));
        var draftRows = Rows(await ReportDesignerTestServices.ReadAsync(source, drafts, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["po-2", "po-1"], orders.Select(row => row[CommerceReportDataSets.ItemIdField]));

        var order = orders[1];
        Assert.Equal("PO-1", order["Number"]);
        Assert.Equal("PartiallyReceived", order["Status"]);
        Assert.Equal("Acme", order["Supplier"]);
        Assert.Equal(InventoryConstants.DefaultLocationId, order["LocationId"]);
        Assert.Equal("USD", order["Currency"]);
        Assert.Equal(2L, order["LineCount"]);
        Assert.Equal(15L, order["Ordered"]);
        Assert.Equal(10L, order["Received"]);
        Assert.Equal(5L, order["Outstanding"]);
        Assert.Equal(45m, order["Total"]);
        Assert.Equal(25m, order["ReceivedCost"]);
        Assert.Equal("Ada", order["Author"]);
        Assert.Equal(0L, orders[0]["LineCount"]);
        Assert.Equal("EUR", orders[0]["Currency"]);

        Assert.Equal(PurchaseOrderLinesReportDataSet.PurchaseOrderCreatedUtcField, schema.DataSet.DefaultDateField);
        Assert.Equal(["l-1", "l-2"], lineRows.Select(row => row["LineId"]));
        Assert.Equal([1L, 2L], lineRows.Select(row => row["Position"]));
        Assert.Equal(["item-mug", "item-cap-red"], lineRows.Select(row => row["StockItemId"]));
        Assert.All(lineRows, row => Assert.Equal("USD", row["Currency"]));
        Assert.Equal(2.5m, lineRows[0]["UnitCost"]);
        Assert.Equal(25m, lineRows[0]["LineTotal"]);
        Assert.Equal(5L, lineRows[1]["Outstanding"]);

        Assert.Equal(["po-2"], dateRows.Select(row => row[CommerceReportDataSets.ItemIdField]));

        // The draft has no lines.
        Assert.Empty(draftRows);
    }

    [Fact]
    public async Task Query_StockAlerts_SaysWhetherTheEmailWentOut()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var forCap = Query(CommerceReportDataSets.InventorySource.StockAlerts);
        forCap.Conditions.Add(new DataCondition { Field = StockItemsReportDataSet.ProductIdField, Operator = DataFilterOperator.In, Values = ["product-cap"], IsJoinKey = true });

        // Act
        var rows = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.InventorySource.StockAlerts), TestContext.Current.CancellationToken));
        var capRows = Rows(await ReportDesignerTestServices.ReadAsync(source, forCap, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["item-mug", "item-cap-red"], rows.Select(row => row["StockItemId"]));
        Assert.Equal([false, true], rows.Select(row => row["Waiting"]));
        Assert.Equal(_start.AddDays(2), rows[0]["SentUtc"]);
        Assert.All(rows, row => Assert.False(string.IsNullOrEmpty((string)row[CommerceReportDataSets.ItemIdField])));
        var cap = Assert.Single(capRows);
        Assert.Equal("red", cap["VariantId"]);
    }

    [Fact]
    public async Task Query_Locations_ListsThemInOrder_AndFiltersByKey()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var shop = Query(CommerceReportDataSets.InventorySource.Locations);
        shop.Conditions.Add(new DataCondition { Field = CommerceReportDataSets.ItemIdField, Operator = DataFilterOperator.In, Values = ["shop"], IsJoinKey = true });
        var none = Query(CommerceReportDataSets.InventorySource.Locations);
        none.Conditions.Add(new DataCondition { Field = CommerceReportDataSets.ItemIdField, Operator = DataFilterOperator.In, Values = [], IsJoinKey = true });

        // Act
        var rows = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.InventorySource.Locations), TestContext.Current.CancellationToken));
        var shopRows = Rows(await ReportDesignerTestServices.ReadAsync(source, shop, TestContext.Current.CancellationToken));
        var noRows = Rows(await ReportDesignerTestServices.ReadAsync(source, none, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal([InventoryConstants.DefaultLocationId, "shop"], rows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal([1L, 2L], rows.Select(row => row["Position"]));
        Assert.Equal([true, false], rows.Select(row => row["IsDefault"]));
        Assert.Equal("Main location", rows[0]["Name"]);

        var location = Assert.Single(shopRows);
        Assert.Equal("Shop", location["Name"]);
        Assert.Equal("1 High Street", location["Address"]);
        Assert.Empty(noRows);
    }

    [Fact]
    public async Task RecipeReports_RunOverTheInventoryDataSource()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var designs = ReportRecipeDesigns.Load("CrestApps.OrchardCore.Inventory", "inventory-report-builder-reports.recipe.json");

        // Act
        var results = await ReportRecipeDesigns.RunAsync(designs, _user, _start.AddDays(10), new TestDataSourceEntry(InventoryReportDataSource.Name, Source(session)));

        // Assert
        Assert.Equal(["inventory-stock-on-hand", "inventory-stock-movements"], designs.Select(design => design.ItemId));
        Assert.All(designs, design => Assert.Equal("Store", design.Category));

        // The archived item is left out, and the cap, with the fewest units available, comes first.
        var stock = results["inventory-stock-on-hand"];
        Assert.Equal(2, stock.Rows.Count);
        Assert.Equal("Cap, Red", ReportRecipeDesigns.Value(stock, 0, "item"));

        // The mug received 10 units and sold 2, and the cap was corrected by 3. Moving the mug to the shop changes neither.
        var movements = results["inventory-stock-movements"];
        Assert.Equal(2, movements.Rows.Count);
        Assert.Equal("Mug", ReportRecipeDesigns.Value(movements, 0, "item"));
        Assert.Equal(10m, Convert.ToDecimal(ReportRecipeDesigns.Value(movements, 0, "received"), CultureInfo.InvariantCulture));
        Assert.Equal(2m, Convert.ToDecimal(ReportRecipeDesigns.Value(movements, 0, "sold"), CultureInfo.InvariantCulture));
        Assert.Equal(10m, Convert.ToDecimal(ReportRecipeDesigns.Value(movements, 0, "onhand"), CultureInfo.InvariantCulture));
        Assert.Equal("Cap, Red", ReportRecipeDesigns.Value(movements, 1, "item"));
        Assert.Equal(3m, Convert.ToDecimal(ReportRecipeDesigns.Value(movements, 1, "corrections"), CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task ReportIndexMigrations_CanRunAgain()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(_store.Configuration, transaction);

        // Act
        var inventory = await new InventoryMigrations(Mock.Of<IContentDefinitionManager>()) { SchemaBuilder = schemaBuilder }.UpdateFrom1Async();
        var purchaseOrders = await new PurchaseOrderMigrations { SchemaBuilder = schemaBuilder }.UpdateFrom1Async();
        var alerts = await new StockAlertMigrations { SchemaBuilder = schemaBuilder }.UpdateFrom1Async();

        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, inventory);
        Assert.Equal(2, purchaseOrders);
        Assert.Equal(2, alerts);
    }

    [Fact]
    public async Task DataSourcesStartups_RegisterTheDataSourceAndTheFeatureDataSets()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var services = new ServiceCollection();

        services.AddSingleton(session);
        services.AddSingleton(Authorization([InventoryPermissions.ViewInventory.Name]));
        services.AddSingleton(Locations());
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>));

        // Act
        new DataSourcesStartup().ConfigureServices(services);
        new PurchaseOrdersDataSourcesStartup().ConfigureServices(services);

        await using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredService<InventoryReportDataSource>();
        var dataSets = await source.GetDataSetsAsync(new DataSourceContext { User = _user }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(CommerceReportDataSets.InventorySource.Name, InventoryReportDataSource.Name);
        Assert.Contains(dataSets, dataSet => dataSet.Name == CommerceReportDataSets.InventorySource.PurchaseOrderLines);
        Assert.DoesNotContain(dataSets, dataSet => dataSet.Name == CommerceReportDataSets.InventorySource.StockAlerts);
        Assert.Equal(7, dataSets.Count);
    }

    private static StockMovement Movement(string id, string stockItemId, StockMovementKind kind, int quantity, int day, string locationId, string orderId = null, string purchaseOrderId = null, string reason = null)
    {
        return new StockMovement
        {
            ItemId = id,
            StockItemId = stockItemId,
            Kind = kind,
            Quantity = quantity,
            OnHandAfter = quantity,
            LocationId = locationId,
            OrderId = orderId,
            PurchaseOrderId = purchaseOrderId,
            Reason = reason,
            ActorId = "u1",
            ActorName = "Ada",
            Utc = _start.AddDays(day),
        };
    }

    private static Task SaveAsync(ISession session, object record)
    {
        return session.SaveAsync(record, false, InventoryConstants.CollectionName, TestContext.Current.CancellationToken);
    }

    private static InventoryReportDataSource Source(ISession session, string[] grants = null)
    {
        var authorizationService = Authorization(grants ?? [InventoryPermissions.ViewInventory.Name]);

        return new InventoryReportDataSource(
        [
            new StockItemsReportDataSet(session, authorizationService, new PassThroughStringLocalizer<StockItemsReportDataSet>()),
            new StockLevelsReportDataSet(session, authorizationService, new PassThroughStringLocalizer<StockLevelsReportDataSet>()),
            new StockMovementsReportDataSet(session, authorizationService, new PassThroughStringLocalizer<StockMovementsReportDataSet>()),
            new StockReservationsReportDataSet(session, authorizationService, new PassThroughStringLocalizer<StockReservationsReportDataSet>()),
            new LocationsReportDataSet(Locations(), authorizationService, new PassThroughStringLocalizer<LocationsReportDataSet>()),
            new PurchaseOrdersReportDataSet(session, authorizationService, new PassThroughStringLocalizer<PurchaseOrdersReportDataSet>()),
            new PurchaseOrderLinesReportDataSet(session, authorizationService, new PassThroughStringLocalizer<PurchaseOrderLinesReportDataSet>()),
            new StockAlertsReportDataSet(session, authorizationService, new PassThroughStringLocalizer<StockAlertsReportDataSet>()),
        ]);
    }

    private static IInventoryLocationService Locations()
    {
        var locations = new Mock<IInventoryLocationService>();

        locations
            .Setup(service => service.GetLocationsAsync())
            .ReturnsAsync(new InventorySettings
            {
                Locations = [new InventoryLocation { LocationId = "shop", Name = "Shop", Address = "1 High Street" }],
            }.GetLocations());

        return locations.Object;
    }

    private static IAuthorizationService Authorization(string[] grants)
    {
        var authorizationService = new Mock<IAuthorizationService>();

        authorizationService
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync((ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements) =>
                requirements.OfType<PermissionRequirement>().All(requirement => grants.Contains(requirement.Permission.Name))
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed());

        return authorizationService.Object;
    }

    private static DataCondition Condition(string field, DataFilterOperator filterOperator, params object[] values)
    {
        return new DataCondition
        {
            Field = field,
            Operator = filterOperator,
            Values = values,
        };
    }

    private static DataSourceQuery Query(string dataSet, params string[] fields)
    {
        return new DataSourceQuery
        {
            DataSet = dataSet,
            Fields = new HashSet<string>(fields, StringComparer.Ordinal),
            MaxRows = 100,
            Context = new DataSourceContext { User = _user },
        };
    }

    private static List<Dictionary<string, object>> Rows(DataRowSet table)
    {
        return table.Rows
            .Select(row => table.Fields.Select((field, index) => (field.Name, Value: row[index])).ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal))
            .ToList();
    }
}
