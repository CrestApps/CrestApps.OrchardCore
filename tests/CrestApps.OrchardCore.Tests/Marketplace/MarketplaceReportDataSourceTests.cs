using System.Globalization;
using System.Security.Claims;
using CrestApps.OrchardCore.Marketplace;
using CrestApps.OrchardCore.Marketplace.Indexes;
using CrestApps.OrchardCore.Marketplace.Migrations;
using CrestApps.OrchardCore.Marketplace.Models;
using CrestApps.OrchardCore.Marketplace.Reports.DataSources;
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

namespace CrestApps.OrchardCore.Tests.Marketplace;

/// <summary>
/// The marketplace data source reads real vendors, earnings and payouts from a SQLite store, so the index queries, the
/// fields they expose, the push-down of dates, join keys and statuses, the grouping in the database, and the permission
/// they require are all exercised end to end.
/// </summary>
public sealed class MarketplaceReportDataSourceTests : IAsyncLifetime
{
    private static readonly ClaimsPrincipal _user = ReportDesignerPrincipals.User("u1", "Manager");
    private static readonly DateTime _start = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"marketplace-reports-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_databasePath};Pooling=False"));
        _store.RegisterIndexes([new VendorIndexProvider(), new VendorEarningIndexProvider(), new VendorPayoutIndexProvider()], MarketplaceConstants.CollectionName);

        await _store.InitializeAsync(cancellationToken);
        await _store.InitializeCollectionAsync(MarketplaceConstants.CollectionName, cancellationToken);

        await using (var session = _store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(cancellationToken);
            var schemaBuilder = new SchemaBuilder(_store.Configuration, transaction);

            await Migrations(schemaBuilder).CreateAsync();

            await transaction.CommitAsync(cancellationToken);
        }

        await using (var session = _store.CreateSession())
        {
            await session.SaveAsync(new Vendor
            {
                ItemId = "vendor-1",
                Name = "Pottery Co",
                Email = "sales@pottery.example",
                UserIds = ["user-a", "user-b"],
                UserNames = ["ann", "bob"],
                CommissionPercent = 12m,
                Status = VendorStatus.Active,
                Country = "US",
                StripeAccountId = "acct_private",
                StripeAccountRequestKey = "key_private",
                PayoutsEnabled = true,
                CreatedUtc = _start,
                ModifiedUtc = _start,
            }, false, MarketplaceConstants.CollectionName, cancellationToken);

            await session.SaveAsync(new Vendor
            {
                ItemId = "vendor-2",
                Name = "Weaving Guild",
                Status = VendorStatus.Suspended,
                CreatedUtc = _start.AddDays(1),
                ModifiedUtc = _start.AddDays(1),
            }, false, MarketplaceConstants.CollectionName, cancellationToken);

            // Four earnings, one a day: three sales and a refund.
            await session.SaveAsync(Earning(0, "vendor-1", "order-1", VendorEarningKind.Sale, 100m, 12m, VendorEarningStatus.PaidOut, "payout-1"), false, MarketplaceConstants.CollectionName, cancellationToken);
            await session.SaveAsync(Earning(1, "vendor-1", "order-2", VendorEarningKind.Sale, 50m, 6m, VendorEarningStatus.Available, null), false, MarketplaceConstants.CollectionName, cancellationToken);
            await session.SaveAsync(Earning(2, "vendor-2", "order-2", VendorEarningKind.Sale, 30m, 3m, VendorEarningStatus.Held, null), false, MarketplaceConstants.CollectionName, cancellationToken);
            await session.SaveAsync(Earning(3, "vendor-1", "order-1", VendorEarningKind.Refund, -20m, -2.4m, VendorEarningStatus.Held, null), false, MarketplaceConstants.CollectionName, cancellationToken);

            await session.SaveAsync(new VendorPayout
            {
                ItemId = "payout-1",
                VendorId = "vendor-1",
                OrderId = "order-1",
                OrderNumber = "N-1",
                Amount = 88m,
                Currency = "USD",
                EarningIds = ["earning-0"],
                Status = VendorPayoutStatus.Paid,
                TransferId = "tr_private",
                CreatedUtc = _start.AddDays(1),
            }, false, MarketplaceConstants.CollectionName, cancellationToken);

            await session.SaveAsync(new VendorPayout
            {
                ItemId = "payout-2",
                VendorId = "vendor-2",
                OrderId = "order-2",
                OrderNumber = "N-2",
                Amount = 27m,
                Currency = "USD",
                EarningIds = ["earning-2"],
                Status = VendorPayoutStatus.Failed,
                Failure = "Private gateway failure",
                CreatedUtc = _start.AddDays(2),
            }, false, MarketplaceConstants.CollectionName, cancellationToken);

            await session.SaveChangesAsync(cancellationToken);
        }
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task WithoutManageMarketplace_NothingIsExposed()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session, grants: []);
        var context = new DataSourceContext { User = _user };

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.MarketplaceSource.Earnings, context, TestContext.Current.CancellationToken);
        var vendors = await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.MarketplaceSource.Vendors), TestContext.Current.CancellationToken);
        var earnings = await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.MarketplaceSource.Earnings), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(dataSets);
        Assert.Null(schema);
        Assert.Empty(vendors.Rows);
        Assert.Empty(earnings.Rows);
    }

    [Fact]
    public async Task WithoutAUser_NothingIsExposed()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var context = new DataSourceContext();
        var query = Query(CommerceReportDataSets.MarketplaceSource.Payouts);
        query.Context = context;

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.MarketplaceSource.Vendors, context, TestContext.Current.CancellationToken);
        var table = await ReportDesignerTestServices.ReadAsync(source, query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(dataSets);
        Assert.Null(schema);
        Assert.Empty(table.Rows);
    }

    [Fact]
    public async Task Schema_DeclaresKeysAndReferences_AndNeverExposesPayoutAccounts()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var context = new DataSourceContext { User = _user };

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var vendors = await source.GetSchemaAsync(CommerceReportDataSets.MarketplaceSource.Vendors, context, TestContext.Current.CancellationToken);
        var earnings = await source.GetSchemaAsync(CommerceReportDataSets.MarketplaceSource.Earnings, context, TestContext.Current.CancellationToken);
        var payouts = await source.GetSchemaAsync(CommerceReportDataSets.MarketplaceSource.Payouts, context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [
                CommerceReportDataSets.MarketplaceSource.Vendors,
                CommerceReportDataSets.MarketplaceSource.Earnings,
                CommerceReportDataSets.MarketplaceSource.Payouts,
            ],
            dataSets.Select(dataSet => dataSet.Name));

        Assert.Null(vendors.DataSet.DefaultDateField);
        Assert.True(vendors.FindField(CommerceReportDataSets.ItemIdField).IsKeyFilterable);
        Assert.DoesNotContain(vendors.Fields, field => field.Name is "StripeAccountId" or "StripeAccountRequestKey" or "UserIds");

        Assert.Equal(VendorEarningsReportDataSet.CreatedUtcField, earnings.DataSet.DefaultDateField);
        Assert.True(earnings.FindField("VendorId").IsKeyFilterable);
        Assert.True(earnings.FindField("OrderId").IsKeyFilterable);
        var vendor = Assert.Single(earnings.FindField("VendorId").References);
        Assert.Equal(CommerceReportDataSets.MarketplaceSource.Name, vendor.Source);
        Assert.Equal(CommerceReportDataSets.MarketplaceSource.Vendors, vendor.DataSet);
        Assert.Equal(CommerceReportDataSets.ItemIdField, vendor.Field);
        var payout = Assert.Single(earnings.FindField("PayoutId").References);
        Assert.Equal(CommerceReportDataSets.MarketplaceSource.Payouts, payout.DataSet);
        Assert.False(string.IsNullOrEmpty(earnings.FindField("Net").Description));
        Assert.NotNull(earnings.FindField("Currency"));

        Assert.Equal(VendorPayoutsReportDataSet.CreatedUtcField, payouts.DataSet.DefaultDateField);
        Assert.True(payouts.FindField("VendorId").IsKeyFilterable);
        Assert.False(payouts.FindField("OrderId").IsKeyFilterable);
        Assert.NotNull(payouts.FindField("Currency"));
        Assert.DoesNotContain(payouts.Fields, field => field.Name is "TransferId" or "Failure" or "EarningIds");
    }

    [Fact]
    public async Task Query_Vendors_ReturnTypedValues_NewestFirst()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);

        // Act
        var table = await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.MarketplaceSource.Vendors), TestContext.Current.CancellationToken);
        var rows = Rows(table);

        // Assert
        Assert.Equal(["vendor-2", "vendor-1"], rows.Select(row => row[CommerceReportDataSets.ItemIdField]));

        var pottery = rows[1];
        Assert.Equal("Pottery Co", pottery["Name"]);
        Assert.Equal("sales@pottery.example", pottery["Email"]);
        Assert.Equal("Active", pottery["Status"]);
        Assert.Equal(12m, pottery["CommissionPercent"]);
        Assert.Equal(2L, pottery["UserCount"]);
        Assert.Equal("ann, bob", pottery["UserNames"]);
        Assert.Equal(true, pottery["HasPayoutAccount"]);
        Assert.Equal(true, pottery["PayoutsEnabled"]);

        var guild = rows[0];
        Assert.Equal("Suspended", guild["Status"]);
        Assert.Null(guild["CommissionPercent"]);
        Assert.Equal(false, guild["HasPayoutAccount"]);

        Assert.DoesNotContain(table.Rows.SelectMany(row => row), value => value is string text && text.Contains("private", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Query_EarningsAndPayouts_ReturnTypedValues_NewestFirst()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);

        // Act
        var earnings = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.MarketplaceSource.Earnings), TestContext.Current.CancellationToken));
        var payoutTable = await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.MarketplaceSource.Payouts), TestContext.Current.CancellationToken);
        var payouts = Rows(payoutTable);

        // Assert
        Assert.Equal(["earning-3", "earning-2", "earning-1", "earning-0"], earnings.Select(row => row[CommerceReportDataSets.ItemIdField]));

        var sale = earnings[3];
        Assert.Equal("vendor-1", sale["VendorId"]);
        Assert.Equal("order-1", sale["OrderId"]);
        Assert.Equal("N-1", sale["OrderNumber"]);
        Assert.Equal("line-0", sale["LineId"]);
        Assert.Equal("Vase", sale["Title"]);
        Assert.Equal("Sale", sale["Kind"]);
        Assert.Equal("PaidOut", sale["Status"]);
        Assert.Equal("payout-1", sale["PayoutId"]);
        Assert.Equal("USD", sale["Currency"]);
        Assert.Equal(100m, sale["Gross"]);
        Assert.Equal(12m, sale["Commission"]);
        Assert.Equal(88m, sale["Net"]);
        Assert.Equal(12m, sale["CommissionPercent"]);
        Assert.Equal(_start, sale["CreatedUtc"]);
        Assert.Equal(DateTimeKind.Utc, Assert.IsType<DateTime>(sale["ReleasedUtc"]).Kind);

        var refund = earnings[0];
        Assert.Equal("Refund", refund["Kind"]);
        Assert.Equal(-20m, refund["Gross"]);
        Assert.Equal(-17.6m, refund["Net"]);
        Assert.Null(refund["ReleasedUtc"]);
        Assert.Null(refund["PayoutId"]);

        Assert.Equal(["payout-2", "payout-1"], payouts.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal("Failed", payouts[0]["Status"]);
        Assert.Equal("Paid", payouts[1]["Status"]);
        Assert.Equal(88m, payouts[1]["Amount"]);
        Assert.Equal("USD", payouts[1]["Currency"]);
        Assert.Equal(1L, payouts[1]["EarningCount"]);
        Assert.Equal("order-1", payouts[1]["OrderId"]);
        Assert.DoesNotContain(payoutTable.Rows.SelectMany(row => row), value => value is string text && text.Contains("private", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Query_PushDownNeverDropsAMatchingRow()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var dates = Query(CommerceReportDataSets.MarketplaceSource.Earnings, CommerceReportDataSets.ItemIdField);
        dates.Conditions.Add(Condition(VendorEarningsReportDataSet.CreatedUtcField, DataFilterOperator.Between, _start.AddDays(1), _start.AddDays(2)));
        var keys = Query(CommerceReportDataSets.MarketplaceSource.Earnings, CommerceReportDataSets.ItemIdField);
        keys.Conditions.Add(new DataCondition { Field = "VendorId", Operator = DataFilterOperator.In, Values = ["vendor-1"], IsJoinKey = true });
        var status = Query(CommerceReportDataSets.MarketplaceSource.Earnings, CommerceReportDataSets.ItemIdField);
        status.Conditions.Add(Condition("Status", DataFilterOperator.In, "held"));
        var payouts = Query(CommerceReportDataSets.MarketplaceSource.Payouts, CommerceReportDataSets.ItemIdField);
        payouts.Conditions.Add(new DataCondition { Field = "VendorId", Operator = DataFilterOperator.In, Values = ["vendor-2"], IsJoinKey = true });
        var vendors = Query(CommerceReportDataSets.MarketplaceSource.Vendors, CommerceReportDataSets.ItemIdField);
        vendors.Conditions.Add(Condition("Status", DataFilterOperator.Equals, "Suspended"));
        var none = Query(CommerceReportDataSets.MarketplaceSource.Earnings, CommerceReportDataSets.ItemIdField);
        none.Conditions.Add(new DataCondition { Field = CommerceReportDataSets.ItemIdField, Operator = DataFilterOperator.In, Values = [], IsJoinKey = true });

        // Act
        var dateRows = Rows(await ReportDesignerTestServices.ReadAsync(source, dates, TestContext.Current.CancellationToken));
        var keyRows = Rows(await ReportDesignerTestServices.ReadAsync(source, keys, TestContext.Current.CancellationToken));
        var statusRows = Rows(await ReportDesignerTestServices.ReadAsync(source, status, TestContext.Current.CancellationToken));
        var payoutRows = Rows(await ReportDesignerTestServices.ReadAsync(source, payouts, TestContext.Current.CancellationToken));
        var vendorRows = Rows(await ReportDesignerTestServices.ReadAsync(source, vendors, TestContext.Current.CancellationToken));
        var noRows = Rows(await ReportDesignerTestServices.ReadAsync(source, none, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["earning-2", "earning-1"], dateRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal(["earning-3", "earning-1", "earning-0"], keyRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal(["earning-3", "earning-2"], statusRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal(["payout-2"], payoutRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal(["vendor-2"], vendorRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Empty(noRows);
    }

    [Fact]
    public async Task Aggregate_CountsEarningsByStatus_AndDeclinesTheVendorName()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var byStatus = new DataAggregateQuery
        {
            DataSet = CommerceReportDataSets.MarketplaceSource.Earnings,
            Groups = [new DataAggregateGroup { Field = "Status" }],
            Measures = [new DataAggregateMeasure { Function = DataAggregate.Count }],
            Context = new DataSourceContext { User = _user },
        };
        var byName = new DataAggregateQuery
        {
            DataSet = CommerceReportDataSets.MarketplaceSource.Vendors,
            Groups = [new DataAggregateGroup { Field = "Name" }],
            Measures = [new DataAggregateMeasure { Function = DataAggregate.Count }],
            Context = new DataSourceContext { User = _user },
        };

        // Act
        var statuses = await ((IDataAggregateSource)source).AggregateAsync(byStatus, TestContext.Current.CancellationToken);
        var names = await ((IDataAggregateSource)source).AggregateAsync(byName, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(statuses);
        var counts = statuses.ToDictionary(row => (string)row[0], row => Convert.ToInt64(row[1], CultureInfo.InvariantCulture));
        Assert.Equal(2L, counts["Held"]);
        Assert.Equal(1L, counts["Available"]);
        Assert.Equal(1L, counts["PaidOut"]);

        // The index keeps a shortened name, so the grouping is left to the report.
        Assert.Null(names);
    }

    [Fact]
    public async Task ReportIndexMigration_CanRunAgain()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(_store.Configuration, transaction);

        // Act
        var first = await Migrations(schemaBuilder).UpdateFrom1Async();
        var second = await Migrations(schemaBuilder).UpdateFrom1Async();

        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, first);
        Assert.Equal(2, second);
    }

    [Fact]
    public async Task DataSourcesStartup_RegistersTheDataSource()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var services = new ServiceCollection();

        services.AddSingleton(session);
        services.AddSingleton(Authorization([MarketplacePermissions.ManageMarketplace.Name]));
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>));

        // Act
        new DataSourcesStartup().ConfigureServices(services);

        await using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredService<MarketplaceReportDataSource>();
        var dataSets = await source.GetDataSetsAsync(new DataSourceContext { User = _user }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(CommerceReportDataSets.MarketplaceSource.Name, MarketplaceReportDataSource.Name);
        Assert.Equal(3, dataSets.Count);
    }

    private static VendorEarning Earning(int day, string vendorId, string orderId, VendorEarningKind kind, decimal gross, decimal commission, VendorEarningStatus status, string payoutId)
    {
        var recorded = _start.AddDays(day);

        return new VendorEarning
        {
            ItemId = $"earning-{day}",
            VendorId = vendorId,
            OrderId = orderId,
            OrderNumber = "N-" + orderId[^1..],
            LineId = $"line-{day}",
            Title = "Vase",
            Currency = "USD",
            Gross = gross,
            CommissionPercent = vendorId == "vendor-1" ? 12m : 10m,
            Commission = commission,
            Net = gross - commission,
            Kind = kind,
            Status = status,
            HoldUntilUtc = recorded.AddDays(7),
            ReleasedUtc = status == VendorEarningStatus.Held ? null : recorded.AddDays(7),
            PayoutId = payoutId,
            CreatedUtc = recorded,
        };
    }

    private static MarketplaceMigrations Migrations(ISchemaBuilder schemaBuilder)
    {
        return new MarketplaceMigrations(new Mock<IContentDefinitionManager>().Object)
        {
            SchemaBuilder = schemaBuilder,
        };
    }

    private static MarketplaceReportDataSource Source(ISession session, string[] grants = null)
    {
        var authorizationService = Authorization(grants ?? [MarketplacePermissions.ManageMarketplace.Name]);

        return new MarketplaceReportDataSource(
        [
            new VendorsReportDataSet(session, authorizationService, new PassThroughStringLocalizer<VendorsReportDataSet>()),
            new VendorEarningsReportDataSet(session, authorizationService, new PassThroughStringLocalizer<VendorEarningsReportDataSet>()),
            new VendorPayoutsReportDataSet(session, authorizationService, new PassThroughStringLocalizer<VendorPayoutsReportDataSet>()),
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
