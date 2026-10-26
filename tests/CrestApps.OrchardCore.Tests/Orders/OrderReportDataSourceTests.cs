using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Orders;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Indexes;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Migrations;
using CrestApps.OrchardCore.Orders.Reports.DataSources;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Designer.Services;
using CrestApps.OrchardCore.Reports.Designer.ViewModels;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.DataSources;
using OrchardCore.Security;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Orders;

/// <summary>
/// The orders data source reads real orders, returns and gift cards from a SQLite store, so the index queries, the
/// fields they expose, the push-down of dates, join keys and statuses, the grouping in the database, and the permissions
/// they require are all exercised end to end.
/// </summary>
public sealed class OrderReportDataSourceTests : IAsyncLifetime
{
    private static readonly ClaimsPrincipal _user = ReportDesignerPrincipals.User("u1", "Manager");
    private static readonly DateTime _start = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"order-reports-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_databasePath};Pooling=False"));
        _store.RegisterIndexes([new OrderIndexProvider(), new OrderReturnIndexProvider(), new GiftCardIndexProvider()], OrderConstants.CollectionName);

        await _store.InitializeAsync(cancellationToken);
        await _store.InitializeCollectionAsync(OrderConstants.CollectionName, cancellationToken);

        await using (var session = _store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(cancellationToken);
            var schemaBuilder = new SchemaBuilder(_store.Configuration, transaction);

            await new OrderMigrations { SchemaBuilder = schemaBuilder }.CreateAsync();
            await new OrderReturnMigrations { SchemaBuilder = schemaBuilder }.CreateAsync();

            await schemaBuilder.CreateMapIndexTableAsync<GiftCardIndex>(table => table
                .Column<string>("ItemId", column => column.WithLength(26))
                .Column<string>("NormalizedCode", column => column.WithLength(32))
                .Column<string>("LastCharacters", column => column.WithLength(8))
                .Column<string>("RecipientEmail", column => column.WithLength(255))
                .Column<decimal>("Balance")
                .Column<string>("Currency", column => column.WithLength(3))
                .Column<bool>("IsEnabled")
                .Column<DateTime>("ExpiresUtc", column => column.Nullable())
                .Column<string>("OrderId", column => column.WithLength(26))
                .Column<DateTime>("CreatedUtc"),
                collection: OrderConstants.CollectionName);

            await transaction.CommitAsync(cancellationToken);
        }

        await using (var session = _store.CreateSession())
        {
            // Four orders, one a day: two paid online by customer-1, one rung up at a register, and one canceled.
            for (var day = 0; day < 4; day++)
            {
                await session.SaveAsync(Order(day), false, OrderConstants.CollectionName, cancellationToken);
            }

            await session.SaveAsync(new OrderReturn
            {
                ItemId = "return-1",
                Number = "R-1",
                OrderId = "order-1",
                OrderNumber = "N-1",
                UserId = "user-1",
                CustomerName = "Ada",
                Currency = "USD",
                Status = OrderReturnStatus.Received,
                Resolution = OrderReturnResolution.Refund,
                RefundedAmount = 20m,
                Lines =
                [
                    new OrderReturnLine { LineId = "line-1a", Title = "Mug", Quantity = 2, UnitPrice = 10m, Restocked = true },
                ],
                CreatedUtc = _start.AddDays(2),
            }, false, OrderConstants.CollectionName, cancellationToken);

            await session.SaveAsync(new GiftCard
            {
                ItemId = "card-1",
                Code = "ABCD-EFGH-JKLM-NPQR",
                InitialValue = 50m,
                Balance = 30m,
                Currency = "USD",
                RecipientEmail = "Gift@Example.com",
                OrderId = "order-0",
                Activity =
                [
                    new GiftCardActivity { CreatedUtc = _start.AddDays(1), Amount = -20m, Balance = 30m },
                ],
                CreatedUtc = _start,
                UpdatedUtc = _start.AddDays(1),
            }, false, OrderConstants.CollectionName, cancellationToken);

            await session.SaveChangesAsync(cancellationToken);
        }
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task WithoutManageOrders_NothingIsExposed()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session, grants: []);
        var context = new DataSourceContext { User = _user };

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.OrderSource.Orders, context, TestContext.Current.CancellationToken);
        var table = await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.OrderSource.Orders), TestContext.Current.CancellationToken);
        var lines = await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.OrderSource.OrderLines), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(dataSets);
        Assert.Null(schema);
        Assert.Empty(table.Rows);
        Assert.Empty(lines.Rows);
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
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.OrderSource.Orders, context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(dataSets);
        Assert.Null(schema);
    }

    [Fact]
    public async Task ManageOrdersAlone_HidesTheGiftCards()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session, grants: [OrderPermissions.ManageOrders.Name]);
        var context = new DataSourceContext { User = _user };

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.OrderSource.GiftCards, context, TestContext.Current.CancellationToken);

        // Assert
        Assert.DoesNotContain(dataSets, dataSet => dataSet.Name == CommerceReportDataSets.OrderSource.GiftCards);
        Assert.Contains(dataSets, dataSet => dataSet.Name == CommerceReportDataSets.OrderSource.Returns);
        Assert.Null(schema);
    }

    [Fact]
    public async Task Schema_Orders_DeclaresKeysAndReferences_AndNeverExposesPrivateDetails()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var context = new DataSourceContext { User = _user };

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.OrderSource.Orders, context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [
                CommerceReportDataSets.OrderSource.Orders,
                CommerceReportDataSets.OrderSource.OrderLines,
                CommerceReportDataSets.OrderSource.OrderRefunds,
                CommerceReportDataSets.OrderSource.OrderDisputes,
                CommerceReportDataSets.OrderSource.OrderDiscounts,
                CommerceReportDataSets.OrderSource.OrderTaxes,
                CommerceReportDataSets.OrderSource.Returns,
                CommerceReportDataSets.OrderSource.GiftCards,
            ],
            dataSets.Select(dataSet => dataSet.Name));
        Assert.Equal(OrdersReportDataSet.CreatedUtcField, schema.DataSet.DefaultDateField);
        Assert.True(schema.FindField(CommerceReportDataSets.ItemIdField).IsIdentifier);
        Assert.True(schema.FindField(CommerceReportDataSets.ItemIdField).IsKeyFilterable);
        Assert.True(schema.FindField("CustomerId").IsKeyFilterable);

        var customer = Assert.Single(schema.FindField("CustomerId").References);
        Assert.Equal(CommerceReportDataSets.CustomerSource.Name, customer.Source);
        Assert.Equal(CommerceReportDataSets.CustomerSource.Customers, customer.DataSet);
        Assert.Equal(CommerceReportDataSets.ItemIdField, customer.Field);

        var user = Assert.Single(schema.FindField("UserId").References);
        Assert.Equal(ReportsConstants.UsersDataSource, user.Source);
        Assert.Equal(ReportsConstants.UserIdField, user.Field);

        Assert.Equal(DataFieldType.Decimal, schema.FindField("Total").Type);
        Assert.False(string.IsNullOrEmpty(schema.FindField("Total").Description));
        Assert.DoesNotContain(schema.Fields, field => field.Name is "CustomerPhone" or "CheckoutDetails" or "Notes" or "BillingAddressLine1" or "CancelNote");
    }

    [Fact]
    public async Task Query_Orders_ReturnsTypedValues_NewestFirst()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);

        // Act
        var rows = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.OrderSource.Orders), TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["order-3", "order-2", "order-1", "order-0"], rows.Select(row => row[CommerceReportDataSets.ItemIdField]));

        var paid = rows[2];
        Assert.Equal("N-1", paid["OrderNumber"]);
        Assert.Equal("Paid", paid["Status"]);
        Assert.Equal(OrderChannels.Online, paid["Channel"]);
        Assert.Equal("USD", paid["Currency"]);
        Assert.Equal(110m, paid["Total"]);
        Assert.Equal(100m, paid["Subtotal"]);
        Assert.Equal(5m, paid["DiscountTotal"]);
        Assert.Equal(15m, paid["TaxAmount"]);
        Assert.Equal(10m, paid["RefundedAmount"]);
        Assert.Equal(100m, paid["NetAmount"]);
        Assert.Equal("PartiallyRefunded", paid["RefundStatus"]);
        Assert.Equal(2L, paid["LineCount"]);
        Assert.Equal(3L, paid["ItemCount"]);
        Assert.Equal("customer-1", paid["CustomerId"]);
        Assert.Equal("Ada", paid["CustomerName"]);
        Assert.Equal("DE", paid["BillingCountry"]);
        Assert.Equal(false, paid["IsGuest"]);
        Assert.Equal(false, paid["IsAnonymized"]);
        Assert.Equal(DateTimeKind.Utc, Assert.IsType<DateTime>(paid["PaidUtc"]).Kind);

        var counter = rows[1];
        Assert.Equal(OrderChannels.PointOfSale, counter["Channel"]);
        Assert.Equal("register-1", counter["RegisterId"]);
        Assert.Equal(true, counter["IsGuest"]);

        var canceled = rows[0];
        Assert.Equal("Canceled", canceled["Status"]);
        Assert.Null(canceled["Total"]);
        Assert.Null(canceled["NetAmount"]);
        Assert.Null(canceled["PaidUtc"]);

        var anonymized = rows[3];
        Assert.Equal(true, anonymized["IsAnonymized"]);
        Assert.Null(anonymized["CustomerName"]);
        Assert.Null(anonymized["CustomerEmail"]);
        Assert.Equal("DE", anonymized["BillingCountry"]);
    }

    [Fact]
    public async Task Query_Orders_PushDownNeverDropsAMatchingRow()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var dates = Query(CommerceReportDataSets.OrderSource.Orders, CommerceReportDataSets.ItemIdField);
        dates.Conditions.Add(Condition(OrdersReportDataSet.CreatedUtcField, DataFilterOperator.Between, _start.AddDays(1), _start.AddDays(2)));
        var keys = Query(CommerceReportDataSets.OrderSource.Orders, CommerceReportDataSets.ItemIdField);
        keys.Conditions.Add(new DataCondition { Field = "CustomerId", Operator = DataFilterOperator.In, Values = ["customer-1"], IsJoinKey = true });
        var status = Query(CommerceReportDataSets.OrderSource.Orders, CommerceReportDataSets.ItemIdField);
        status.Conditions.Add(Condition("Status", DataFilterOperator.In, "paid"));
        var channel = Query(CommerceReportDataSets.OrderSource.Orders, CommerceReportDataSets.ItemIdField);
        channel.Conditions.Add(Condition("Channel", DataFilterOperator.Equals, "POS"));
        var none = Query(CommerceReportDataSets.OrderSource.Orders, CommerceReportDataSets.ItemIdField);
        none.Conditions.Add(new DataCondition { Field = CommerceReportDataSets.ItemIdField, Operator = DataFilterOperator.In, Values = [], IsJoinKey = true });

        // Act
        var dateRows = Rows(await ReportDesignerTestServices.ReadAsync(source, dates, TestContext.Current.CancellationToken));
        var keyRows = Rows(await ReportDesignerTestServices.ReadAsync(source, keys, TestContext.Current.CancellationToken));
        var statusRows = Rows(await ReportDesignerTestServices.ReadAsync(source, status, TestContext.Current.CancellationToken));
        var channelRows = Rows(await ReportDesignerTestServices.ReadAsync(source, channel, TestContext.Current.CancellationToken));
        var noRows = Rows(await ReportDesignerTestServices.ReadAsync(source, none, TestContext.Current.CancellationToken));

        // Assert
        // The bounds are inclusive, so the rows on them stay; the report applies the strict comparison itself.
        Assert.Equal(["order-2", "order-1"], dateRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal(["order-1", "order-0"], keyRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal(["order-2", "order-1", "order-0"], statusRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal(["order-2"], channelRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Empty(noRows);
    }

    [Fact]
    public async Task Query_Orders_StopsAtMaxRows()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var query = Query(CommerceReportDataSets.OrderSource.Orders, CommerceReportDataSets.ItemIdField);
        query.MaxRows = 2;

        // Act
        var table = await ReportDesignerTestServices.ReadAsync(source, query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["order-3", "order-2"], Rows(table).Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.True(table.Truncated);
    }

    [Fact]
    public async Task Aggregate_Orders_CountsByStatusInTheDatabase()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var query = new DataAggregateQuery
        {
            DataSet = CommerceReportDataSets.OrderSource.Orders,
            Groups = [new DataAggregateGroup { Field = "Status" }],
            Measures = [new DataAggregateMeasure { Function = DataAggregate.Count }],
            Context = new DataSourceContext { User = _user },
        };

        // Act
        var groups = await ((IDataAggregateSource)source).AggregateAsync(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(groups);
        var counts = groups.ToDictionary(row => (string)row[0], row => (long)row[1]);
        Assert.Equal(3L, counts["Paid"]);
        Assert.Equal(1L, counts["Canceled"]);
    }

    [Fact]
    public async Task Aggregate_OrderLines_Declines()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var query = new DataAggregateQuery
        {
            DataSet = CommerceReportDataSets.OrderSource.OrderLines,
            Groups = [new DataAggregateGroup { Field = "OrderStatus" }],
            Measures = [new DataAggregateMeasure { Function = DataAggregate.Count }],
            Context = new DataSourceContext { User = _user },
        };

        // Act
        var groups = await ((IDataAggregateSource)source).AggregateAsync(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(groups);
    }

    [Fact]
    public async Task Query_OrderLines_GivesOneRowPerLineWithItsOrder()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var query = Query(CommerceReportDataSets.OrderSource.OrderLines);
        query.Conditions.Add(new DataCondition { Field = OrderEntryReportDataSet<OrderLine>.OrderIdField, Operator = DataFilterOperator.In, Values = ["order-1"], IsJoinKey = true });

        // Act
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.OrderSource.OrderLines, new DataSourceContext { User = _user }, TestContext.Current.CancellationToken);
        var rows = Rows(await ReportDesignerTestServices.ReadAsync(source, query, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(OrderEntryReportDataSet<OrderLine>.OrderCreatedUtcField, schema.DataSet.DefaultDateField);
        Assert.True(schema.FindField(OrderEntryReportDataSet<OrderLine>.OrderIdField).IsKeyFilterable);
        var product = Assert.Single(schema.FindField("ProductId").References);
        Assert.Equal(CommerceReportDataSets.ProductSource.Name, product.Source);
        Assert.Equal(CommerceReportDataSets.ContentItemIdField, product.Field);

        Assert.Equal(["line-1a", "line-1b"], rows.Select(row => row["LineId"]));
        Assert.Equal([1L, 2L], rows.Select(row => row["Position"]));
        Assert.All(rows, row => Assert.Equal("order-1", row[OrderEntryReportDataSet<OrderLine>.OrderIdField]));
        Assert.All(rows, row => Assert.Equal("Paid", row["OrderStatus"]));
        Assert.All(rows, row => Assert.Equal("USD", row["Currency"]));
        Assert.Equal(2L, rows[0]["Quantity"]);
        Assert.Equal(40m, rows[0]["LineTotal"]);
        Assert.Equal("product-mug", rows[0]["ProductId"]);
        Assert.Equal("MUG-1", rows[0]["Sku"]);
        Assert.Equal("Good", rows[0]["ProductType"]);
        Assert.Equal("cat-1, cat-2", rows[0]["Categories"]);
    }

    [Fact]
    public async Task Query_RefundsDiscountsAndTaxes_ComeFromTheirOrders()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);

        // Act
        var refunds = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.OrderSource.OrderRefunds), TestContext.Current.CancellationToken));
        var discounts = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.OrderSource.OrderDiscounts), TestContext.Current.CancellationToken));
        var taxes = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.OrderSource.OrderTaxes), TestContext.Current.CancellationToken));
        var disputes = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.OrderSource.OrderDisputes), TestContext.Current.CancellationToken));

        // Assert
        var refund = Assert.Single(refunds);
        Assert.Equal("order-1", refund[OrderEntryReportDataSet<OrderRefund>.OrderIdField]);
        Assert.Equal(10m, refund["Amount"]);
        Assert.Equal("Damaged", refund["Reason"]);

        Assert.Equal(["SAVE5", "SAVE5", "SAVE5", "SAVE5"], discounts.Select(row => row["Code"]));
        Assert.Equal(["DE-VAT", "DE-VAT", "DE-VAT"], taxes.Select(row => row["JurisdictionId"]));
        Assert.Equal(15m, taxes[0]["TaxAmount"]);
        Assert.Equal(0.19m, taxes[0]["Rate"]);
        Assert.Equal("DE", taxes[0]["BillingCountry"]);

        var dispute = Assert.Single(disputes);
        Assert.Equal("dp-1", dispute["DisputeReference"]);
        Assert.Equal(false, dispute["IsClosed"]);
    }

    [Fact]
    public async Task Query_Returns_AndGiftCards_NeverExposeTheGiftCardCode()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var context = new DataSourceContext { User = _user };

        // Act
        var returns = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.OrderSource.Returns), TestContext.Current.CancellationToken));
        var cardSchema = await source.GetSchemaAsync(CommerceReportDataSets.OrderSource.GiftCards, context, TestContext.Current.CancellationToken);
        var cards = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.OrderSource.GiftCards), TestContext.Current.CancellationToken));

        // Assert
        var orderReturn = Assert.Single(returns);
        Assert.Equal("Received", orderReturn["Status"]);
        Assert.Equal("order-1", orderReturn["OrderId"]);
        Assert.Equal(2L, orderReturn["Quantity"]);
        Assert.Equal(2L, orderReturn["RestockedUnits"]);
        Assert.Equal(20m, orderReturn["ReturnedAmount"]);

        Assert.DoesNotContain(cardSchema.Fields, field => field.Name is "Code" or "NormalizedCode" or "Note");
        var card = Assert.Single(cards);
        Assert.Equal("NPQR", card["LastCharacters"]);
        Assert.Equal(30m, card["Balance"]);
        Assert.Equal(20m, card["SpentAmount"]);
        Assert.Equal(1L, card["UseCount"]);
        Assert.DoesNotContain(card.Values, value => value is string text && text.Contains("ABCD", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RecipeReports_RunOverTheOrdersDataSource()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var planner = ReportDesignerTestServices.Planner(new TestDataSourceEntry(OrderReportDataSource.Name, source));
        var engine = new ReportQueryEngine(planner, ReportDesignerTestServices.Formatter, new ReportQueryGate(), new PassThroughStringLocalizer<ReportQueryEngine>());
        var designs = RecipeReports();
        var results = new Dictionary<string, ReportQueryResult>(StringComparer.Ordinal);

        // Act
        foreach (var design in designs)
        {
            var context = ReportDesignerTestServices.Context(_start.AddDays(10));
            context.DataSourceContext.User = _user;

            var plan = await planner.PlanAsync(design.Query, context.DataSourceContext, TestContext.Current.CancellationToken);

            Assert.True(plan.IsValid, $"{design.ItemId}: {string.Join(" ", plan.Errors)}");

            foreach (var grouping in ReportDesignDocumentBuilder.GetGroupings(design.Visuals, ReportQueryEngine.DescribeColumns(plan)))
            {
                context.Groupings.Add(grouping);
            }

            var result = await engine.ExecuteAsync(plan, context, TestContext.Current.CancellationToken);

            Assert.Empty(ReportDesignerTestServices.DocumentBuilder().Validate(design.Visuals, result.Columns));
            results[design.ItemId] = result;
        }

        // Assert
        Assert.Equal(
            ["store-sales-by-day", "store-sales-by-product", "store-sales-by-country", "store-tax-by-jurisdiction", "store-discounts", "store-refunds", "store-disputes", "store-orders-to-ship"],
            designs.Select(design => design.ItemId));
        Assert.All(designs, design => Assert.Equal("Store", design.Category));

        // Three orders were paid, one a day; the canceled one never was.
        Assert.Equal(3, results["store-sales-by-day"].Rows.Count);
        Assert.Equal(2, results["store-sales-by-product"].Rows.Count);
        Assert.Single(results["store-tax-by-jurisdiction"].Rows);
        Assert.Single(results["store-discounts"].Rows);
        Assert.Single(results["store-refunds"].Rows);
        Assert.Single(results["store-disputes"].Rows);
        Assert.Equal(2, results["store-orders-to-ship"].Rows.Count);
    }

    [Fact]
    public async Task ReportIndexMigration_CanRunAgain()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(_store.Configuration, transaction);

        // Act
        var orders = await new OrderMigrations { SchemaBuilder = schemaBuilder }.UpdateFrom1Async();
        var returns = await new OrderReturnMigrations { SchemaBuilder = schemaBuilder }.UpdateFrom1Async();

        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, orders);
        Assert.Equal(2, returns);
    }

    [Fact]
    public async Task DataSourcesStartups_RegisterTheDataSourceAndTheFeatureDataSets()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var services = new ServiceCollection();

        services.AddSingleton(session);
        services.AddSingleton(Authorization([OrderPermissions.ManageOrders.Name, OrderPermissions.ManageGiftCards.Name]));
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>));

        // Act
        new DataSourcesStartup().ConfigureServices(services);
        new ReturnsDataSourcesStartup().ConfigureServices(services);

        await using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredService<OrderReportDataSource>();
        var dataSets = await source.GetDataSetsAsync(new DataSourceContext { User = _user }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(CommerceReportDataSets.OrderSource.Name, OrderReportDataSource.Name);
        Assert.Contains(dataSets, dataSet => dataSet.Name == CommerceReportDataSets.OrderSource.Returns);
        Assert.DoesNotContain(dataSets, dataSet => dataSet.Name == CommerceReportDataSets.OrderSource.GiftCards);
        Assert.Equal(7, dataSets.Count);
    }

    private static List<ReportDesign> RecipeReports()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        var recipe = JsonNode.Parse(File.ReadAllText(Path.Combine(directory.FullName, "src", "Modules", "CrestApps.OrchardCore.Orders", "Recipes", "commerce-report-builder-reports.recipe.json")));
        var steps = recipe["steps"].AsArray();

        Assert.Contains(ReportsConstants.BuilderFeature, steps[0]["enable"].AsArray().Select(feature => (string)feature));

        return steps[1]["Reports"].AsArray()
            .Select(node =>
            {
                var design = node.Deserialize<ReportDesign>(ReportDesignerJson.Options);

                design.Query = ReportDesignNormalizer.Normalize(design.Query);
                design.Visuals = ReportDesignNormalizer.Normalize(design.Visuals);

                return design;
            })
            .ToList();
    }

    private static Order Order(int day)
    {
        var placed = _start.AddDays(day);

        var order = new Order
        {
            ItemId = $"order-{day}",
            OrderNumber = $"N-{day}",
            Currency = "USD",
            Channel = day == 2 ? OrderChannels.PointOfSale : OrderChannels.Online,
            Status = day == 3 ? OrderStatus.Canceled : OrderStatus.Paid,
            UserId = day == 2 ? null : "user-1",
            CustomerId = day is 0 or 1 ? "customer-1" : "customer-2",
            CustomerName = "Ada",
            CustomerEmail = "ada@example.com",
            CustomerPhone = "+15550000000",
            BillingAddress = new Address { Country = "DE", Region = "BE", City = "Berlin", AddressLine1 = "1 Private Street" },
            RegisterId = day == 2 ? "register-1" : null,
            Shipping = new OrderShipping { Name = "Standard", ProviderName = "Flat", Amount = 5m },
            FulfillmentStatus = day == 1 ? OrderFulfillmentStatus.Fulfilled : null,
            Subtotal = 100m,
            Total = day == 3 ? null : 110m,
            TaxAmount = day == 3 ? null : 15m,
            Discounts = [new OrderDiscount { Code = "SAVE5", Amount = 5m, ProviderKey = "Coupons" }],
            TaxLines = day == 3 ? [] : [new TaxLine { JurisdictionId = "DE-VAT", JurisdictionName = "Germany", TaxName = "VAT", Rate = 0.19m, TaxableAmount = 79m, TaxAmount = 15m }],
            Lines =
            [
                new OrderLine { LineId = $"line-{day}a", ContentItemId = "product-mug", Title = "Mug", Sku = "MUG-1", Quantity = 2, UnitPrice = 20m, LineTotal = 40m, ProductType = ProductType.Good, CategoryIds = ["cat-1", "cat-2"] },
                new OrderLine { LineId = $"line-{day}b", ContentItemId = "product-cap", Title = "Cap", Sku = "CAP-1", Quantity = 1, UnitPrice = 60m, LineTotal = 60m },
            ],
            CreatedUtc = placed,
            ModifiedUtc = placed,
            PaidUtc = day == 3 ? null : placed.AddMinutes(5),
            CanceledUtc = day == 3 ? placed.AddHours(1) : null,
        };

        if (day == 1)
        {
            order.Refunds = [new OrderRefund { RefundId = "refund-1", Amount = 10m, Currency = "USD", Reason = "Damaged", CreatedUtc = placed.AddDays(1) }];
            order.Disputes = [new OrderDispute { Reference = "dp-1", Gateway = "Stripe", Amount = 110m, Currency = "USD", Status = "needs_response", CreatedUtc = placed.AddDays(2), UpdatedUtc = placed.AddDays(2) }];
        }

        if (day == 0)
        {
            order.Anonymize(placed.AddDays(30));
        }

        return order;
    }

    private static OrderReportDataSource Source(ISession session, string[] grants = null)
    {
        var authorizationService = Authorization(grants ?? [OrderPermissions.ManageOrders.Name, OrderPermissions.ManageGiftCards.Name]);

        return new OrderReportDataSource(
        [
            new OrdersReportDataSet(session, authorizationService, new PassThroughStringLocalizer<OrdersReportDataSet>()),
            new OrderLinesReportDataSet(session, authorizationService, new PassThroughStringLocalizer<OrderLinesReportDataSet>()),
            new OrderRefundsReportDataSet(session, authorizationService, new PassThroughStringLocalizer<OrderRefundsReportDataSet>()),
            new OrderDisputesReportDataSet(session, authorizationService, new PassThroughStringLocalizer<OrderDisputesReportDataSet>()),
            new OrderDiscountsReportDataSet(session, authorizationService, new PassThroughStringLocalizer<OrderDiscountsReportDataSet>()),
            new OrderTaxesReportDataSet(session, authorizationService, new PassThroughStringLocalizer<OrderTaxesReportDataSet>()),
            new ReturnsReportDataSet(session, authorizationService, new PassThroughStringLocalizer<ReturnsReportDataSet>()),
            new GiftCardsReportDataSet(session, authorizationService, new PassThroughStringLocalizer<GiftCardsReportDataSet>()),
        ]);
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
