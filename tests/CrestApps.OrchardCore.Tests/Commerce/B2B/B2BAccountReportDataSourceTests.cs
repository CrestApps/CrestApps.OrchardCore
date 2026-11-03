using System.Security.Claims;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Accounts.B2B;
using CrestApps.OrchardCore.Accounts.B2B.Indexes;
using CrestApps.OrchardCore.Accounts.B2B.Migrations;
using CrestApps.OrchardCore.Accounts.B2B.Models;
using CrestApps.OrchardCore.Accounts.B2B.Reports.DataSources;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module;
using CrestApps.OrchardCore.Tests.Modules.Accounts;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.DataSources;
using OrchardCore.Security;
using OrchardCore.Settings;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Commerce.B2B;

/// <summary>
/// The accounts data source reads real accounts and price lists from a SQLite store, so the index queries, the
/// fields they expose, the push-down of join keys and names, the grouping in the database, and the permission they
/// require are all exercised end to end.
/// </summary>
public sealed class B2BAccountReportDataSourceTests : IAsyncLifetime
{
    private static readonly ClaimsPrincipal _user = ReportDesignerPrincipals.User("u1", "Manager");
    private static readonly DateTime _start = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"account-reports-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_databasePath};Pooling=False"));
        _store.RegisterIndexes([new B2BAccountIndexProvider()]);
        _store.RegisterIndexes([new PriceListIndexProvider()], B2BAccountsConstants.PriceListsCollectionName);

        await _store.InitializeAsync(cancellationToken);
        await _store.InitializeCollectionAsync(B2BAccountsConstants.PriceListsCollectionName, cancellationToken);

        await using (var session = _store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(cancellationToken);
            var schemaBuilder = new SchemaBuilder(_store.Configuration, transaction);

            await new B2BAccountMigrations(new InMemoryContentDefinitionManager()) { SchemaBuilder = schemaBuilder }.CreateAsync();

            await transaction.CommitAsync(cancellationToken);
        }

        await using (var session = _store.CreateSession())
        {
            // Each record is flushed on its own, so the index rows keep the order they were added in.
            await SaveAsync(session, new PriceList
            {
                ItemId = "list-1",
                Name = "Wholesale",
                PercentOff = 10m,
                Currency = "USD",
                Entries =
                [
                    new PriceListEntry { Sku = "SKU-1", UnitPrice = 8.5m },
                    new PriceListEntry { Sku = "SKU-2", UnitPrice = 12m },
                ],
                CreatedUtc = _start,
            }, cancellationToken);

            await SaveAsync(session, new PriceList
            {
                ItemId = "list-2",
                Name = "Partners",
                Currency = "EUR",
                CreatedUtc = _start.AddDays(1),
            }, cancellationToken);

            await SaveAsync(session, TestB2BAccounts.CreateItem("account-1", "Contoso", part =>
            {
                part.TaxId = "DE123456789";
                part.BillingEmail = "billing@contoso.example";
                part.Buyers =
                [
                    new AccountBuyer { UserId = "user-1", UserName = "ada", Email = "ada@contoso.example", Role = AccountBuyerRole.Admin },
                    new AccountBuyer { UserId = "user-2", UserName = "bob", Email = "bob@contoso.example", Role = AccountBuyerRole.Buyer, OrderLimit = 500m },
                ];
                part.Locations = [new AccountLocation { LocationId = "hq", Name = "Head office" }];
                part.Terms = new AccountTerms { NetDays = 30, DepositPercent = 20m, CreditLimit = 10000m, RequirePurchaseOrder = true };
                part.PriceListId = "list-1";
                part.MonthlyBudget = 5000m;
                part.Punchout = new AccountPunchout { Enabled = true, UserId = "user-2", KeyHash = "SECRETHASH", KeyCreatedUtc = _start };
            }, createdUtc: _start), cancellationToken);

            await SaveAsync(session, TestB2BAccounts.CreateItem("account-2", "Fabrikam", part =>
            {
                part.Enabled = false;
                part.Buyers = [new AccountBuyer { UserName = "pending", Email = "pending@fabrikam.example", Role = AccountBuyerRole.Buyer }];
            }, createdUtc: _start.AddDays(1)), cancellationToken);

            await session.SaveChangesAsync(cancellationToken);
        }
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task WithoutManageB2BAccounts_NothingIsExposed()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session, grants: []);
        var context = new DataSourceContext { User = _user };

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.B2BAccountSource.B2BAccounts, context, TestContext.Current.CancellationToken);
        var accounts = await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.B2BAccountSource.B2BAccounts), TestContext.Current.CancellationToken);
        var buyers = await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.B2BAccountSource.B2BBuyers), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(dataSets);
        Assert.Null(schema);
        Assert.Empty(accounts.Rows);
        Assert.Empty(buyers.Rows);
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
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.B2BAccountSource.PriceLists, context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(dataSets);
        Assert.Null(schema);
    }

    [Fact]
    public async Task Schema_B2BAccounts_DeclaresKeysAndReferences_AndNeverExposesThePunchoutKey()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var context = new DataSourceContext { User = _user };

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.B2BAccountSource.B2BAccounts, context, TestContext.Current.CancellationToken);
        var buyers = await source.GetSchemaAsync(CommerceReportDataSets.B2BAccountSource.B2BBuyers, context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [
                CommerceReportDataSets.B2BAccountSource.B2BAccounts,
                CommerceReportDataSets.B2BAccountSource.B2BBuyers,
                CommerceReportDataSets.B2BAccountSource.PriceLists,
                CommerceReportDataSets.B2BAccountSource.PriceListEntries,
            ],
            dataSets.Select(dataSet => dataSet.Name));
        Assert.Null(schema.DataSet.DefaultDateField);
        Assert.True(schema.FindField(CommerceReportDataSets.ItemIdField).IsIdentifier);
        Assert.True(schema.FindField(CommerceReportDataSets.ItemIdField).IsKeyFilterable);

        var priceList = Assert.Single(schema.FindField("PriceListId").References);
        Assert.Equal(CommerceReportDataSets.B2BAccountSource.Name, priceList.Source);
        Assert.Equal(CommerceReportDataSets.B2BAccountSource.PriceLists, priceList.DataSet);
        Assert.Equal(CommerceReportDataSets.ItemIdField, priceList.Field);

        Assert.Equal(DataFieldType.Decimal, schema.FindField("CreditLimit").Type);
        Assert.False(string.IsNullOrEmpty(schema.FindField("CreditLimit").Description));
        Assert.Equal(DataFieldType.Text, schema.FindField("Currency").Type);
        Assert.DoesNotContain(schema.Fields, field => field.Name is "KeyHash" or "PunchoutKeyHash" or "Punchout" or "PunchoutUserId" or "Locations");

        Assert.True(buyers.FindField(B2BBuyersReportDataSet.AccountIdField).IsKeyFilterable);
        Assert.False(buyers.FindField("UserId").IsKeyFilterable);
        Assert.Equal(ReportsConstants.UsersDataSource, Assert.Single(buyers.FindField("UserId").References).Source);
        Assert.Equal(CommerceReportDataSets.B2BAccountSource.B2BAccounts, Assert.Single(buyers.FindField(B2BBuyersReportDataSet.AccountIdField).References).DataSet);
    }

    [Fact]
    public async Task Query_B2BAccounts_ReturnsTypedValues_NewestFirst()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);

        // Act
        var rows = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.B2BAccountSource.B2BAccounts), TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["account-2", "account-1"], rows.Select(row => row[CommerceReportDataSets.ItemIdField]));

        var contoso = rows[1];
        Assert.Equal("Contoso", contoso["Name"]);
        Assert.Equal("DE123456789", contoso["TaxId"]);
        Assert.Equal(true, contoso["IsEnabled"]);
        Assert.Equal(2L, contoso["BuyerCount"]);
        Assert.Equal(1L, contoso["AdminCount"]);
        Assert.Equal(1L, contoso["LocationCount"]);
        Assert.Equal("list-1", contoso["PriceListId"]);
        Assert.Equal(true, contoso["PunchoutEnabled"]);
        Assert.Equal(true, contoso["OffersTerms"]);
        Assert.Equal(30L, contoso["NetDays"]);
        Assert.Equal(20m, contoso["DepositPercent"]);
        Assert.Equal(true, contoso["RequirePurchaseOrder"]);
        Assert.Equal("USD", contoso["Currency"]);
        Assert.Equal(10000m, contoso["CreditLimit"]);
        Assert.Equal(5000m, contoso["MonthlyBudget"]);
        Assert.Equal(DateTimeKind.Utc, Assert.IsType<DateTime>(contoso["CreatedUtc"]).Kind);

        var fabrikam = rows[0];
        Assert.Equal(false, fabrikam["IsEnabled"]);
        Assert.Equal(false, fabrikam["OffersTerms"]);
        Assert.Null(fabrikam["NetDays"]);
        Assert.Null(fabrikam["CreditLimit"]);
        Assert.Null(fabrikam["PriceListId"]);
        Assert.Equal(false, fabrikam["PunchoutEnabled"]);
        Assert.Equal("USD", fabrikam["Currency"]);

        Assert.DoesNotContain(rows.SelectMany(row => row.Values), value => value is string text && text.Contains("SECRET", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Query_B2BAccounts_PushDownNeverDropsAMatchingRow()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var name = Query(CommerceReportDataSets.B2BAccountSource.B2BAccounts, CommerceReportDataSets.ItemIdField);
        name.Conditions.Add(Condition("Name", DataFilterOperator.Equals, "contoso"));
        var keys = Query(CommerceReportDataSets.B2BAccountSource.B2BAccounts, CommerceReportDataSets.ItemIdField);
        keys.Conditions.Add(new DataCondition { Field = CommerceReportDataSets.ItemIdField, Operator = DataFilterOperator.In, Values = ["account-2"], IsJoinKey = true });
        var none = Query(CommerceReportDataSets.B2BAccountSource.B2BAccounts, CommerceReportDataSets.ItemIdField);
        none.Conditions.Add(new DataCondition { Field = CommerceReportDataSets.ItemIdField, Operator = DataFilterOperator.In, Values = [], IsJoinKey = true });

        // Act
        var nameRows = Rows(await ReportDesignerTestServices.ReadAsync(source, name, TestContext.Current.CancellationToken));
        var keyRows = Rows(await ReportDesignerTestServices.ReadAsync(source, keys, TestContext.Current.CancellationToken));
        var noRows = Rows(await ReportDesignerTestServices.ReadAsync(source, none, TestContext.Current.CancellationToken));

        // Assert - the name has no index column, so it is filtered after the read and both accounts are read.
        Assert.Contains("account-1", nameRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal(["account-2"], keyRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Empty(noRows);
    }

    [Fact]
    public async Task Aggregate_B2BAccounts_Declines_SoTheEngineCountsTheRowsItRead()
    {
        // Arrange - an account is a content item read through its published version, which the index can't group.
        await using var session = _store.CreateSession();
        var source = Source(session);
        var query = new DataAggregateQuery
        {
            DataSet = CommerceReportDataSets.B2BAccountSource.B2BAccounts,
            Groups = [new DataAggregateGroup { Field = "IsEnabled" }],
            Measures = [new DataAggregateMeasure { Function = DataAggregate.Count }],
            Context = new DataSourceContext { User = _user },
        };

        // Act
        var groups = await ((IDataAggregateSource)source).AggregateAsync(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(groups);
    }

    [Fact]
    public async Task Query_B2BBuyers_GivesOneRowPerBuyerWithItsAccount()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var contoso = Query(CommerceReportDataSets.B2BAccountSource.B2BBuyers);
        contoso.Conditions.Add(new DataCondition { Field = B2BBuyersReportDataSet.AccountIdField, Operator = DataFilterOperator.In, Values = ["account-1"], IsJoinKey = true });
        var byUser = Query(CommerceReportDataSets.B2BAccountSource.B2BBuyers);
        byUser.Conditions.Add(new DataCondition { Field = "UserId", Operator = DataFilterOperator.In, Values = ["user-2"], IsJoinKey = true });

        // Act
        var rows = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.B2BAccountSource.B2BBuyers), TestContext.Current.CancellationToken));
        var contosoRows = Rows(await ReportDesignerTestServices.ReadAsync(source, contoso, TestContext.Current.CancellationToken));
        var userRows = Rows(await ReportDesignerTestServices.ReadAsync(source, byUser, TestContext.Current.CancellationToken));

        // Assert
        // A buyer without a user is listed too, and each account gives its buyers once.
        Assert.Equal(["account-2", "account-1", "account-1"], rows.Select(row => row[B2BBuyersReportDataSet.AccountIdField]));
        Assert.Equal([null, "user-1", "user-2"], rows.Select(row => row["UserId"]));
        Assert.Equal([1L, 1L, 2L], rows.Select(row => row["Position"]));
        Assert.Equal(["Buyer", "Admin", "Buyer"], rows.Select(row => row["Role"]));
        Assert.Equal([null, null, 500m], rows.Select(row => row["OrderLimit"]));
        Assert.All(rows, row => Assert.Equal("USD", row["Currency"]));
        Assert.Equal("Contoso", rows[1]["AccountName"]);
        Assert.Equal("ada", rows[1]["UserName"]);

        Assert.Equal(["user-1", "user-2"], contosoRows.Select(row => row["UserId"]));

        // The buyer's user is not pushed down, so the read keeps every buyer and the report filters them itself.
        Assert.Contains(userRows, row => Equals(row["UserId"], "user-2"));
    }

    [Fact]
    public async Task Aggregate_B2BBuyers_Declines()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var query = new DataAggregateQuery
        {
            DataSet = CommerceReportDataSets.B2BAccountSource.B2BBuyers,
            Groups = [new DataAggregateGroup { Field = "Role" }],
            Measures = [new DataAggregateMeasure { Function = DataAggregate.Count }],
            Context = new DataSourceContext { User = _user },
        };

        // Act
        var groups = await ((IDataAggregateSource)source).AggregateAsync(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(groups);
    }

    [Fact]
    public async Task Query_PriceListsAndEntries_GivePricesInTheListCurrency()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var entries = Query(CommerceReportDataSets.B2BAccountSource.PriceListEntries);
        entries.Conditions.Add(new DataCondition { Field = PriceListEntriesReportDataSet.PriceListIdField, Operator = DataFilterOperator.In, Values = ["list-1"], IsJoinKey = true });

        // Act
        var lists = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.B2BAccountSource.PriceLists), TestContext.Current.CancellationToken));
        var prices = Rows(await ReportDesignerTestServices.ReadAsync(source, entries, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["list-2", "list-1"], lists.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal("Wholesale", lists[1]["Name"]);
        Assert.Equal(10m, lists[1]["PercentOff"]);
        Assert.Equal("USD", lists[1]["Currency"]);
        Assert.Equal(2L, lists[1]["EntryCount"]);
        Assert.Null(lists[0]["PercentOff"]);
        Assert.Equal(0L, lists[0]["EntryCount"]);

        Assert.Equal(["SKU-1", "SKU-2"], prices.Select(row => row["Sku"]));
        Assert.Equal([8.5m, 12m], prices.Select(row => row["UnitPrice"]));
        Assert.All(prices, row => Assert.Equal("USD", row["Currency"]));
        Assert.All(prices, row => Assert.Equal("Wholesale", row["PriceListName"]));
    }

    [Fact]
    public async Task DataSourcesStartup_RegistersTheDataSourceAndItsDataSets()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var services = new ServiceCollection();

        services.AddSingleton(session);
        services.AddSingleton(Authorization([B2BAccountsPermissions.ManageB2BAccounts.Name]));
        services.AddSingleton(SiteService());
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>));

        // Act
        new DataSourcesStartup().ConfigureServices(services);

        await using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredService<B2BAccountReportDataSource>();
        var dataSets = await source.GetDataSetsAsync(new DataSourceContext { User = _user }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(CommerceReportDataSets.B2BAccountSource.Name, B2BAccountReportDataSource.Name);
        Assert.Equal(4, dataSets.Count);
    }

    private static async Task SaveAsync(ISession session, object item, CancellationToken cancellationToken)
    {
        await session.SaveAsync(item, false, item is PriceList ? B2BAccountsConstants.PriceListsCollectionName : null, cancellationToken);
        await session.FlushAsync(cancellationToken);
    }

    private static B2BAccountReportDataSource Source(ISession session, string[] grants = null)
    {
        var authorizationService = Authorization(grants ?? [B2BAccountsPermissions.ManageB2BAccounts.Name]);
        var siteService = SiteService();

        return new B2BAccountReportDataSource(
        [
            new B2BAccountsReportDataSet(session, authorizationService, siteService, new PassThroughStringLocalizer<B2BAccountsReportDataSet>()),
            new B2BBuyersReportDataSet(session, authorizationService, siteService, new PassThroughStringLocalizer<B2BBuyersReportDataSet>()),
            new PriceListsReportDataSet(session, authorizationService, new PassThroughStringLocalizer<PriceListsReportDataSet>()),
            new PriceListEntriesReportDataSet(session, authorizationService, new PassThroughStringLocalizer<PriceListEntriesReportDataSet>()),
        ]);
    }

    private static ISiteService SiteService()
    {
        var site = new Mock<ISite>();
        site.Setup(value => value.GetOrCreate<CheckoutSettings>()).Returns(new CheckoutSettings { Currency = "USD" });
        var siteService = new Mock<ISiteService>();
        siteService.Setup(value => value.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        return siteService.Object;
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
