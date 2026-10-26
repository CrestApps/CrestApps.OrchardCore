using System.Globalization;
using System.Security.Claims;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Shipping;
using CrestApps.OrchardCore.Shipping.Indexes;
using CrestApps.OrchardCore.Shipping.Migrations;
using CrestApps.OrchardCore.Shipping.Models;
using CrestApps.OrchardCore.Shipping.Reports.DataSources;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.DataSources;
using OrchardCore.Entities;
using OrchardCore.Security;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Shipping;

/// <summary>
/// The shipping data source reads real shipments from a SQLite store, and the shipping zones from their catalog, so the
/// index queries, the fields they expose, the push-down of dates, join keys and statuses, the grouping in the database,
/// and the permissions they require are all exercised end to end.
/// </summary>
public sealed class ShippingReportDataSourceTests : IAsyncLifetime
{
    private static readonly ClaimsPrincipal _user = ReportDesignerPrincipals.User("u1", "Shipper");
    private static readonly DateTime _start = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"shipping-reports-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_databasePath};Pooling=False"));
        _store.RegisterIndexes([new ShipmentIndexProvider()], ShippingConstants.CollectionName);

        await _store.InitializeAsync(cancellationToken);
        await _store.InitializeCollectionAsync(ShippingConstants.CollectionName, cancellationToken);

        await using (var session = _store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(cancellationToken);

            await new ShippingMigrations(Mock.Of<IContentDefinitionManager>()) { SchemaBuilder = new SchemaBuilder(_store.Configuration, transaction) }.CreateAsync();

            await transaction.CommitAsync(cancellationToken);
        }

        await using (var session = _store.CreateSession())
        {
            // Two shipments of the first order, one delivered and one on its way, and one of the second order being packed.
            await SaveAsync(session, new Shipment
            {
                ItemId = "shipment-0",
                OrderId = "order-1",
                OrderNumber = "N-1",
                Number = 1,
                Status = ShipmentStatus.Delivered,
                Carrier = "UPS",
                TrackingNumber = "1Z999",
                TrackingUrl = "https://tracking.example.com/1Z999",
                TrackingStatus = "Delivered",
                Note = "Leave at the back door",
                LabelUrl = "https://labels.example.com/private-label.pdf",
                LabelCost = 7.5m,
                LabelCurrency = "USD",
                Lines =
                [
                    new ShipmentLine { LineId = "line-1a", Title = "Mug", Sku = "MUG-1", Quantity = 2 },
                    new ShipmentLine { LineId = "line-1b", Title = "Cap", Sku = "CAP-1", Quantity = 1 },
                ],
                CreatedUtc = _start,
                ModifiedUtc = _start.AddDays(2),
                ShippedUtc = _start.AddHours(2),
                DeliveredUtc = _start.AddDays(2),
                ShippedEmailSentUtc = _start.AddHours(2),
            });

            await SaveAsync(session, new Shipment
            {
                ItemId = "shipment-1",
                OrderId = "order-1",
                OrderNumber = "N-1",
                Number = 2,
                Status = ShipmentStatus.Shipped,
                Carrier = "USPS",
                TrackingNumber = "9400",
                Lines = [new ShipmentLine { LineId = "line-1c", Title = "Hat", Sku = "HAT-1", Quantity = 1 }],
                CreatedUtc = _start.AddDays(1),
                ModifiedUtc = _start.AddDays(1),
                ShippedUtc = _start.AddDays(1).AddHours(1),
            });

            await SaveAsync(session, new Shipment
            {
                ItemId = "shipment-2",
                OrderId = "order-2",
                OrderNumber = "N-2",
                Number = 1,
                Status = ShipmentStatus.Pending,
                Lines = [new ShipmentLine { LineId = "line-2a", Title = "Mug", Sku = "MUG-1", Quantity = 3 }],
                CreatedUtc = _start.AddDays(2),
                ModifiedUtc = _start.AddDays(2),
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
    public async Task WithoutAnyShippingPermission_NothingIsExposed()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session, grants: []);
        var context = new DataSourceContext { User = _user };

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.ShippingSource.Shipments, context, TestContext.Current.CancellationToken);
        var shipments = await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.ShippingSource.Shipments), TestContext.Current.CancellationToken);
        var zones = await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.ShippingSource.ShippingZones), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(dataSets);
        Assert.Null(schema);
        Assert.Empty(shipments.Rows);
        Assert.Empty(zones.Rows);
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
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.ShippingSource.ShippingZones, context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(dataSets);
        Assert.Null(schema);
    }

    [Fact]
    public async Task FulfillOrdersAlone_HidesTheZonesAndMethods()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session, grants: [ShippingPermissions.FulfillOrders.Name]);
        var context = new DataSourceContext { User = _user };

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var methods = await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.ShippingSource.ShippingMethods), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([CommerceReportDataSets.ShippingSource.Shipments, CommerceReportDataSets.ShippingSource.ShipmentLines], dataSets.Select(dataSet => dataSet.Name));
        Assert.Empty(methods.Rows);
    }

    [Fact]
    public async Task Schema_DeclaresKeysAndReferences_AndNeverExposesNotesOrLinks()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var context = new DataSourceContext { User = _user };

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var shipments = await source.GetSchemaAsync(CommerceReportDataSets.ShippingSource.Shipments, context, TestContext.Current.CancellationToken);
        var lines = await source.GetSchemaAsync(CommerceReportDataSets.ShippingSource.ShipmentLines, context, TestContext.Current.CancellationToken);
        var rows = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.ShippingSource.Shipments), TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(
            [
                CommerceReportDataSets.ShippingSource.Shipments,
                CommerceReportDataSets.ShippingSource.ShipmentLines,
                CommerceReportDataSets.ShippingSource.ShippingZones,
                CommerceReportDataSets.ShippingSource.ShippingMethods,
            ],
            dataSets.Select(dataSet => dataSet.Name));
        Assert.Equal(ShipmentsReportDataSet.CreatedUtcField, shipments.DataSet.DefaultDateField);
        Assert.True(shipments.FindField(CommerceReportDataSets.ItemIdField).IsIdentifier);
        Assert.True(shipments.FindField(CommerceReportDataSets.ItemIdField).IsKeyFilterable);
        Assert.True(shipments.FindField("OrderId").IsKeyFilterable);

        var order = Assert.Single(shipments.FindField("OrderId").References);
        Assert.Equal(CommerceReportDataSets.OrderSource.Name, order.Source);
        Assert.Equal(CommerceReportDataSets.OrderSource.Orders, order.DataSet);
        Assert.Equal(CommerceReportDataSets.ItemIdField, order.Field);

        Assert.Equal(DataFieldType.Decimal, shipments.FindField("LabelCost").Type);
        Assert.False(string.IsNullOrEmpty(shipments.FindField("LabelCost").Description));
        Assert.NotNull(shipments.FindField("Currency"));

        var orderLine = Assert.Single(lines.FindField("LineId").References);
        Assert.Equal(CommerceReportDataSets.OrderSource.OrderLines, orderLine.DataSet);
        Assert.Equal("LineId", orderLine.Field);
        var shipment = Assert.Single(lines.FindField(ShipmentLinesReportDataSet.ShipmentIdField).References);
        Assert.Equal(CommerceReportDataSets.ShippingSource.Name, shipment.Source);
        Assert.Equal(CommerceReportDataSets.ShippingSource.Shipments, shipment.DataSet);

        Assert.DoesNotContain(shipments.Fields, field => field.Name is "Note" or "LabelUrl" or "TrackingUrl" or "TrackingEvents");
        Assert.DoesNotContain(rows.SelectMany(row => row.Values), value => value is string text && (text.Contains("back door", StringComparison.Ordinal) || text.Contains("https://", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Query_Shipments_ReturnsTypedValues_NewestFirst()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);

        // Act
        var rows = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.ShippingSource.Shipments), TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["shipment-2", "shipment-1", "shipment-0"], rows.Select(row => row[CommerceReportDataSets.ItemIdField]));

        var delivered = rows[2];
        Assert.Equal("order-1", delivered["OrderId"]);
        Assert.Equal("N-1", delivered["OrderNumber"]);
        Assert.Equal(1L, delivered["Number"]);
        Assert.Equal("Delivered", delivered["Status"]);
        Assert.Equal("UPS", delivered["Carrier"]);
        Assert.Equal("1Z999", delivered["TrackingNumber"]);
        Assert.Equal(2L, delivered["LineCount"]);
        Assert.Equal(3L, delivered["ItemCount"]);
        Assert.Equal(true, delivered["HasLabel"]);
        Assert.Equal(7.5m, delivered["LabelCost"]);
        Assert.Equal("USD", delivered["Currency"]);
        Assert.Equal(DateTimeKind.Utc, Assert.IsType<DateTime>(delivered["DeliveredUtc"]).Kind);

        var pending = rows[0];
        Assert.Equal("Pending", pending["Status"]);
        Assert.Equal(false, pending["HasLabel"]);
        Assert.Null(pending["LabelCost"]);
        Assert.Null(pending["ShippedUtc"]);
    }

    [Fact]
    public async Task Query_Shipments_PushDownNeverDropsAMatchingRow()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var dates = Query(CommerceReportDataSets.ShippingSource.Shipments, CommerceReportDataSets.ItemIdField);
        dates.Conditions.Add(Condition(ShipmentsReportDataSet.CreatedUtcField, DataFilterOperator.Between, _start, _start.AddDays(1)));
        var keys = Query(CommerceReportDataSets.ShippingSource.Shipments, CommerceReportDataSets.ItemIdField);
        keys.Conditions.Add(new DataCondition { Field = "OrderId", Operator = DataFilterOperator.In, Values = ["order-1"], IsJoinKey = true });
        var status = Query(CommerceReportDataSets.ShippingSource.Shipments, CommerceReportDataSets.ItemIdField);
        status.Conditions.Add(Condition("Status", DataFilterOperator.In, "shipped", "delivered"));
        var none = Query(CommerceReportDataSets.ShippingSource.Shipments, CommerceReportDataSets.ItemIdField);
        none.Conditions.Add(new DataCondition { Field = CommerceReportDataSets.ItemIdField, Operator = DataFilterOperator.In, Values = [], IsJoinKey = true });

        // Act
        var dateRows = Rows(await ReportDesignerTestServices.ReadAsync(source, dates, TestContext.Current.CancellationToken));
        var keyRows = Rows(await ReportDesignerTestServices.ReadAsync(source, keys, TestContext.Current.CancellationToken));
        var statusRows = Rows(await ReportDesignerTestServices.ReadAsync(source, status, TestContext.Current.CancellationToken));
        var noRows = Rows(await ReportDesignerTestServices.ReadAsync(source, none, TestContext.Current.CancellationToken));

        // Assert
        // The bounds are inclusive, so the rows on them stay; the report applies the strict comparison itself.
        Assert.Equal(["shipment-1", "shipment-0"], dateRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal(["shipment-1", "shipment-0"], keyRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal(["shipment-1", "shipment-0"], statusRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Empty(noRows);
    }

    [Fact]
    public async Task Aggregate_Shipments_CountsByStatusInTheDatabase_AndTheLinesDecline()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var byStatus = new DataAggregateQuery
        {
            DataSet = CommerceReportDataSets.ShippingSource.Shipments,
            Groups = [new DataAggregateGroup { Field = "Status" }],
            Measures = [new DataAggregateMeasure { Function = DataAggregate.Count }],
            Context = new DataSourceContext { User = _user },
        };

        var byTracking = new DataAggregateQuery
        {
            DataSet = CommerceReportDataSets.ShippingSource.Shipments,
            Groups = [new DataAggregateGroup { Field = "TrackingNumber" }],
            Measures = [new DataAggregateMeasure { Function = DataAggregate.Count }],
            Context = new DataSourceContext { User = _user },
        };

        var lines = new DataAggregateQuery
        {
            DataSet = CommerceReportDataSets.ShippingSource.ShipmentLines,
            Groups = [new DataAggregateGroup { Field = "ShipmentStatus" }],
            Measures = [new DataAggregateMeasure { Function = DataAggregate.Count }],
            Context = new DataSourceContext { User = _user },
        };

        // Act
        var groups = await ((IDataAggregateSource)source).AggregateAsync(byStatus, TestContext.Current.CancellationToken);
        var trackingGroups = await ((IDataAggregateSource)source).AggregateAsync(byTracking, TestContext.Current.CancellationToken);
        var lineGroups = await ((IDataAggregateSource)source).AggregateAsync(lines, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(groups);
        var counts = groups.ToDictionary(row => (string)row[0], row => (long)row[1]);
        Assert.Equal(1L, counts["Delivered"]);
        Assert.Equal(1L, counts["Shipped"]);
        Assert.Equal(1L, counts["Pending"]);

        // The index keeps the tracking number trimmed, so only the rows can group by it.
        Assert.Null(trackingGroups);
        Assert.Null(lineGroups);
    }

    [Fact]
    public async Task Query_ShipmentLines_GivesOneRowPerLineWithItsShipment()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var query = Query(CommerceReportDataSets.ShippingSource.ShipmentLines);
        query.Conditions.Add(new DataCondition { Field = "OrderId", Operator = DataFilterOperator.In, Values = ["order-1"], IsJoinKey = true });
        var pending = Query(CommerceReportDataSets.ShippingSource.ShipmentLines);
        pending.Conditions.Add(Condition("ShipmentStatus", DataFilterOperator.In, "pending"));

        // Act
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.ShippingSource.ShipmentLines, new DataSourceContext { User = _user }, TestContext.Current.CancellationToken);
        var rows = Rows(await ReportDesignerTestServices.ReadAsync(source, query, TestContext.Current.CancellationToken));
        var pendingRows = Rows(await ReportDesignerTestServices.ReadAsync(source, pending, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(ShipmentLinesReportDataSet.ShipmentCreatedUtcField, schema.DataSet.DefaultDateField);
        Assert.Equal(["line-1c", "line-1a", "line-1b"], rows.Select(row => row["LineId"]));
        Assert.Equal(["shipment-1", "shipment-0", "shipment-0"], rows.Select(row => row[ShipmentLinesReportDataSet.ShipmentIdField]));
        Assert.Equal([1L, 1L, 2L], rows.Select(row => row["Position"]));
        Assert.Equal([1L, 2L, 1L], rows.Select(row => row["Quantity"]));
        Assert.Equal("Delivered", rows[1]["ShipmentStatus"]);
        Assert.Equal("MUG-1", rows[1]["Sku"]);

        var line = Assert.Single(pendingRows);
        Assert.Equal("line-2a", line["LineId"]);
        Assert.Equal(3L, line["Quantity"]);
    }

    [Fact]
    public async Task Query_ZonesAndMethods_ListThemInTheOrderTheyAreTried()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session, grants: [ShippingPermissions.ManageShipping.Name]);
        var europe = Query(CommerceReportDataSets.ShippingSource.ShippingMethods);
        europe.Conditions.Add(new DataCondition { Field = ShippingMethodsReportDataSet.ZoneIdField, Operator = DataFilterOperator.In, Values = ["zone-eu"], IsJoinKey = true });
        var none = Query(CommerceReportDataSets.ShippingSource.ShippingZones);
        none.Conditions.Add(new DataCondition { Field = CommerceReportDataSets.ItemIdField, Operator = DataFilterOperator.In, Values = [], IsJoinKey = true });

        // Act
        var zones = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.ShippingSource.ShippingZones), TestContext.Current.CancellationToken));
        var methods = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.ShippingSource.ShippingMethods), TestContext.Current.CancellationToken));
        var europeMethods = Rows(await ReportDesignerTestServices.ReadAsync(source, europe, TestContext.Current.CancellationToken));
        var noZones = Rows(await ReportDesignerTestServices.ReadAsync(source, none, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["zone-eu", "zone-us", "zone-row"], zones.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal("DE, FR", zones[0]["Countries"]);
        Assert.Null(zones[0]["Regions"]);
        Assert.Equal(2L, zones[0]["MethodCount"]);
        Assert.Equal(1L, zones[0]["EnabledMethodCount"]);
        Assert.Equal("US-AK", zones[1]["Regions"]);
        Assert.Equal(true, zones[2]["RestOfWorld"]);

        Assert.Equal(["standard", "free", "express"], methods.Select(row => row["MethodId"]));
        Assert.Equal(["zone-eu", "zone-eu", "zone-row"], methods.Select(row => row[ShippingMethodsReportDataSet.ZoneIdField]));

        var standard = europeMethods[0];
        Assert.Equal("Europe", standard["ZoneName"]);
        Assert.Equal(ShippingConstants.Providers.Flat, standard["ProviderName"]);
        Assert.Equal("EUR", standard["Currency"]);
        Assert.Equal(4.9m, standard["FlatAmount"]);
        Assert.Equal(1m, standard["PerItemAmount"]);
        Assert.Null(standard["FreeFromSubtotal"]);

        var free = europeMethods[1];
        Assert.Equal(false, free["Enabled"]);
        Assert.Equal(50m, free["FreeFromSubtotal"]);
        Assert.Null(free["FlatAmount"]);

        Assert.Null(methods[2]["FlatAmount"]);
        Assert.Empty(noZones);
    }

    [Fact]
    public async Task RecipeReports_RunOverTheShippingDataSource()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var designs = ReportRecipeDesigns.Load("CrestApps.OrchardCore.Shipping", "shipping-report-builder-reports.recipe.json");

        // Act
        var results = await ReportRecipeDesigns.RunAsync(designs, _user, _start.AddDays(10), new TestDataSourceEntry(ShippingReportDataSource.Name, Source(session)));

        // Assert
        Assert.Equal(["shipping-by-carrier"], designs.Select(design => design.ItemId));
        Assert.All(designs, design => Assert.Equal("Store", design.Category));

        // Two shipments were sent, one by each carrier; the one being packed is left out. The UPS one took two days less
        // the two hours it waited to leave.
        var carriers = results["shipping-by-carrier"];
        Assert.Equal(2, carriers.Rows.Count);
        Assert.Equal("UPS", ReportRecipeDesigns.Value(carriers, 0, "carrier"));
        Assert.Equal(3m, Convert.ToDecimal(ReportRecipeDesigns.Value(carriers, 0, "units"), CultureInfo.InvariantCulture));
        Assert.Equal(1m, Convert.ToDecimal(ReportRecipeDesigns.Value(carriers, 0, "delivered"), CultureInfo.InvariantCulture));
        Assert.Equal(1.92m, Math.Round(Convert.ToDecimal(ReportRecipeDesigns.Value(carriers, 0, "deliverydays"), CultureInfo.InvariantCulture), 2));
        Assert.Equal("USPS", ReportRecipeDesigns.Value(carriers, 1, "carrier"));
        Assert.Equal(0m, Convert.ToDecimal(ReportRecipeDesigns.Value(carriers, 1, "delivered"), CultureInfo.InvariantCulture));
        Assert.Null(ReportRecipeDesigns.Value(carriers, 1, "deliverydays"));
    }

    [Fact]
    public async Task ReportIndexMigration_CanRunAgain()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);

        // Act
        var version = await new ShippingMigrations(Mock.Of<IContentDefinitionManager>()) { SchemaBuilder = new SchemaBuilder(_store.Configuration, transaction) }.UpdateFrom1Async();

        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, version);
    }

    [Fact]
    public async Task DataSourcesStartup_RegistersTheDataSourceAndItsDataSets()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var services = new ServiceCollection();

        services.AddSingleton(session);
        services.AddSingleton(Authorization([ShippingPermissions.FulfillOrders.Name, ShippingPermissions.ManageShipping.Name]));
        services.AddSingleton(Zones());
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>));

        // Act
        new DataSourcesStartup().ConfigureServices(services);

        await using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredService<ShippingReportDataSource>();
        var dataSets = await source.GetDataSetsAsync(new DataSourceContext { User = _user }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(CommerceReportDataSets.ShippingSource.Name, ShippingReportDataSource.Name);
        Assert.Equal(4, dataSets.Count);
    }

    private static Task SaveAsync(ISession session, Shipment shipment)
    {
        return session.SaveAsync(shipment, false, ShippingConstants.CollectionName, TestContext.Current.CancellationToken);
    }

    private static ShippingReportDataSource Source(ISession session, string[] grants = null)
    {
        var authorizationService = Authorization(grants ?? [ShippingPermissions.FulfillOrders.Name, ShippingPermissions.ManageShipping.Name]);
        var zones = Zones();

        return new ShippingReportDataSource(
        [
            new ShipmentsReportDataSet(session, authorizationService, new PassThroughStringLocalizer<ShipmentsReportDataSet>()),
            new ShipmentLinesReportDataSet(session, authorizationService, new PassThroughStringLocalizer<ShipmentLinesReportDataSet>()),
            new ShippingZonesReportDataSet(zones, authorizationService, new PassThroughStringLocalizer<ShippingZonesReportDataSet>()),
            new ShippingMethodsReportDataSet(zones, authorizationService, new PassThroughStringLocalizer<ShippingMethodsReportDataSet>()),
        ]);
    }

    private static INamedCatalog<ShippingZone> Zones()
    {
        var standard = new ShippingMethod { MethodId = "standard", Name = "Standard", ProviderName = ShippingConstants.Providers.Flat, Currency = "EUR", Position = 1 };
        standard.Put(new FlatRateSettings { Amount = 4.9m, PerItemAmount = 1m });

        var free = new ShippingMethod { MethodId = "free", Name = "Free", ProviderName = ShippingConstants.Providers.Free, Currency = "EUR", Position = 2, Enabled = false };
        free.Put(new FreeShippingSettings { MinimumSubtotal = 50m });

        var express = new ShippingMethod { MethodId = "express", Name = "Express", ProviderName = ShippingConstants.Providers.Table, Currency = "USD" };

        var catalog = new Mock<INamedCatalog<ShippingZone>>();

        catalog
            .Setup(value => value.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ShippingZone { ItemId = "zone-row", Name = "Rest of the world", RestOfWorld = true, Methods = [express], CreatedUtc = _start },
                new ShippingZone { ItemId = "zone-us", Name = "United States", Position = 2, Countries = ["US"], Regions = ["US-AK"], CreatedUtc = _start },
                new ShippingZone { ItemId = "zone-eu", Name = "Europe", Position = 1, Countries = ["DE", "FR"], Methods = [free, standard], CreatedUtc = _start },
            ]);

        return catalog.Object;
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
