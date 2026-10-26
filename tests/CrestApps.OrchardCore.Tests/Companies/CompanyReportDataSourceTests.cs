using System.Security.Claims;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Companies;
using CrestApps.OrchardCore.Companies.Indexes;
using CrestApps.OrchardCore.Companies.Migrations;
using CrestApps.OrchardCore.Companies.Models;
using CrestApps.OrchardCore.Companies.Reports.DataSources;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module;
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

namespace CrestApps.OrchardCore.Tests.Companies;

/// <summary>
/// The companies data source reads real companies and price lists from a SQLite store, so the index queries, the
/// fields they expose, the push-down of join keys and names, the grouping in the database, and the permission they
/// require are all exercised end to end.
/// </summary>
public sealed class CompanyReportDataSourceTests : IAsyncLifetime
{
    private static readonly ClaimsPrincipal _user = ReportDesignerPrincipals.User("u1", "Manager");
    private static readonly DateTime _start = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"company-reports-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_databasePath};Pooling=False"));
        _store.RegisterIndexes([new CompanyIndexProvider(), new PriceListIndexProvider()], CompaniesConstants.CollectionName);

        await _store.InitializeAsync(cancellationToken);
        await _store.InitializeCollectionAsync(CompaniesConstants.CollectionName, cancellationToken);

        await using (var session = _store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(cancellationToken);
            var schemaBuilder = new SchemaBuilder(_store.Configuration, transaction);

            await new CompanyMigrations { SchemaBuilder = schemaBuilder }.CreateAsync();

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

            await SaveAsync(session, new Company
            {
                ItemId = "company-1",
                Name = "Contoso",
                TaxId = "DE123456789",
                BillingEmail = "billing@contoso.example",
                Buyers =
                [
                    new CompanyBuyer { UserId = "user-1", UserName = "ada", Email = "ada@contoso.example", Role = CompanyBuyerRole.Admin },
                    new CompanyBuyer { UserId = "user-2", UserName = "bob", Email = "bob@contoso.example", Role = CompanyBuyerRole.Buyer, OrderLimit = 500m },
                ],
                Locations = [new CompanyLocation { LocationId = "hq", Name = "Head office" }],
                Terms = new CompanyTerms { NetDays = 30, DepositPercent = 20m, CreditLimit = 10000m, RequirePurchaseOrder = true },
                PriceListId = "list-1",
                MonthlyBudget = 5000m,
                Punchout = new CompanyPunchout { Enabled = true, UserId = "user-2", KeyHash = "SECRETHASH", KeyCreatedUtc = _start },
                CreatedUtc = _start,
            }, cancellationToken);

            await SaveAsync(session, new Company
            {
                ItemId = "company-2",
                Name = "Fabrikam",
                IsEnabled = false,
                Buyers = [new CompanyBuyer { UserName = "pending", Email = "pending@fabrikam.example", Role = CompanyBuyerRole.Buyer }],
                CreatedUtc = _start.AddDays(1),
            }, cancellationToken);

            await session.SaveChangesAsync(cancellationToken);
        }
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task WithoutManageCompanies_NothingIsExposed()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session, grants: []);
        var context = new DataSourceContext { User = _user };

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.CompanySource.Companies, context, TestContext.Current.CancellationToken);
        var companies = await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.CompanySource.Companies), TestContext.Current.CancellationToken);
        var buyers = await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.CompanySource.CompanyBuyers), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(dataSets);
        Assert.Null(schema);
        Assert.Empty(companies.Rows);
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
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.CompanySource.PriceLists, context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(dataSets);
        Assert.Null(schema);
    }

    [Fact]
    public async Task Schema_Companies_DeclaresKeysAndReferences_AndNeverExposesThePunchoutKey()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var context = new DataSourceContext { User = _user };

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.CompanySource.Companies, context, TestContext.Current.CancellationToken);
        var buyers = await source.GetSchemaAsync(CommerceReportDataSets.CompanySource.CompanyBuyers, context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [
                CommerceReportDataSets.CompanySource.Companies,
                CommerceReportDataSets.CompanySource.CompanyBuyers,
                CommerceReportDataSets.CompanySource.PriceLists,
                CommerceReportDataSets.CompanySource.PriceListEntries,
            ],
            dataSets.Select(dataSet => dataSet.Name));
        Assert.Null(schema.DataSet.DefaultDateField);
        Assert.True(schema.FindField(CommerceReportDataSets.ItemIdField).IsIdentifier);
        Assert.True(schema.FindField(CommerceReportDataSets.ItemIdField).IsKeyFilterable);

        var priceList = Assert.Single(schema.FindField("PriceListId").References);
        Assert.Equal(CommerceReportDataSets.CompanySource.Name, priceList.Source);
        Assert.Equal(CommerceReportDataSets.CompanySource.PriceLists, priceList.DataSet);
        Assert.Equal(CommerceReportDataSets.ItemIdField, priceList.Field);

        Assert.Equal(DataFieldType.Decimal, schema.FindField("CreditLimit").Type);
        Assert.False(string.IsNullOrEmpty(schema.FindField("CreditLimit").Description));
        Assert.Equal(DataFieldType.Text, schema.FindField("Currency").Type);
        Assert.DoesNotContain(schema.Fields, field => field.Name is "KeyHash" or "PunchoutKeyHash" or "Punchout" or "PunchoutUserId" or "Locations");

        Assert.True(buyers.FindField(CompanyBuyersReportDataSet.CompanyIdField).IsKeyFilterable);
        Assert.False(buyers.FindField("UserId").IsKeyFilterable);
        Assert.Equal(ReportsConstants.UsersDataSource, Assert.Single(buyers.FindField("UserId").References).Source);
        Assert.Equal(CommerceReportDataSets.CompanySource.Companies, Assert.Single(buyers.FindField(CompanyBuyersReportDataSet.CompanyIdField).References).DataSet);
    }

    [Fact]
    public async Task Query_Companies_ReturnsTypedValues_NewestFirst()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);

        // Act
        var rows = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.CompanySource.Companies), TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["company-2", "company-1"], rows.Select(row => row[CommerceReportDataSets.ItemIdField]));

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
    public async Task Query_Companies_PushDownNeverDropsAMatchingRow()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var name = Query(CommerceReportDataSets.CompanySource.Companies, CommerceReportDataSets.ItemIdField);
        name.Conditions.Add(Condition("Name", DataFilterOperator.Equals, "contoso"));
        var keys = Query(CommerceReportDataSets.CompanySource.Companies, CommerceReportDataSets.ItemIdField);
        keys.Conditions.Add(new DataCondition { Field = CommerceReportDataSets.ItemIdField, Operator = DataFilterOperator.In, Values = ["company-2"], IsJoinKey = true });
        var none = Query(CommerceReportDataSets.CompanySource.Companies, CommerceReportDataSets.ItemIdField);
        none.Conditions.Add(new DataCondition { Field = CommerceReportDataSets.ItemIdField, Operator = DataFilterOperator.In, Values = [], IsJoinKey = true });

        // Act
        var nameRows = Rows(await ReportDesignerTestServices.ReadAsync(source, name, TestContext.Current.CancellationToken));
        var keyRows = Rows(await ReportDesignerTestServices.ReadAsync(source, keys, TestContext.Current.CancellationToken));
        var noRows = Rows(await ReportDesignerTestServices.ReadAsync(source, none, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["company-1"], nameRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal(["company-2"], keyRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Empty(noRows);
    }

    [Fact]
    public async Task Aggregate_Companies_CountsByStatusInTheDatabase()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var query = new DataAggregateQuery
        {
            DataSet = CommerceReportDataSets.CompanySource.Companies,
            Groups = [new DataAggregateGroup { Field = "IsEnabled" }],
            Measures = [new DataAggregateMeasure { Function = DataAggregate.Count }],
            Context = new DataSourceContext { User = _user },
        };

        // Act
        var groups = await ((IDataAggregateSource)source).AggregateAsync(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(groups);
        var counts = groups.ToDictionary(row => Convert.ToBoolean(row[0]), row => (long)row[1]);
        Assert.Equal(1L, counts[true]);
        Assert.Equal(1L, counts[false]);
    }

    [Fact]
    public async Task Query_CompanyBuyers_GivesOneRowPerBuyerWithItsCompany()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var contoso = Query(CommerceReportDataSets.CompanySource.CompanyBuyers);
        contoso.Conditions.Add(new DataCondition { Field = CompanyBuyersReportDataSet.CompanyIdField, Operator = DataFilterOperator.In, Values = ["company-1"], IsJoinKey = true });
        var byUser = Query(CommerceReportDataSets.CompanySource.CompanyBuyers);
        byUser.Conditions.Add(new DataCondition { Field = "UserId", Operator = DataFilterOperator.In, Values = ["user-2"], IsJoinKey = true });

        // Act
        var rows = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.CompanySource.CompanyBuyers), TestContext.Current.CancellationToken));
        var contosoRows = Rows(await ReportDesignerTestServices.ReadAsync(source, contoso, TestContext.Current.CancellationToken));
        var userRows = Rows(await ReportDesignerTestServices.ReadAsync(source, byUser, TestContext.Current.CancellationToken));

        // Assert
        // A buyer without a user is listed too, and each company gives its buyers once.
        Assert.Equal(["company-2", "company-1", "company-1"], rows.Select(row => row[CompanyBuyersReportDataSet.CompanyIdField]));
        Assert.Equal([null, "user-1", "user-2"], rows.Select(row => row["UserId"]));
        Assert.Equal([1L, 1L, 2L], rows.Select(row => row["Position"]));
        Assert.Equal(["Buyer", "Admin", "Buyer"], rows.Select(row => row["Role"]));
        Assert.Equal([null, null, 500m], rows.Select(row => row["OrderLimit"]));
        Assert.All(rows, row => Assert.Equal("USD", row["Currency"]));
        Assert.Equal("Contoso", rows[1]["CompanyName"]);
        Assert.Equal("ada", rows[1]["UserName"]);

        Assert.Equal(["user-1", "user-2"], contosoRows.Select(row => row["UserId"]));

        // The buyer's user is not pushed down, so the read keeps every buyer and the report filters them itself.
        Assert.Contains(userRows, row => Equals(row["UserId"], "user-2"));
    }

    [Fact]
    public async Task Aggregate_CompanyBuyers_Declines()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var query = new DataAggregateQuery
        {
            DataSet = CommerceReportDataSets.CompanySource.CompanyBuyers,
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
        var entries = Query(CommerceReportDataSets.CompanySource.PriceListEntries);
        entries.Conditions.Add(new DataCondition { Field = PriceListEntriesReportDataSet.PriceListIdField, Operator = DataFilterOperator.In, Values = ["list-1"], IsJoinKey = true });

        // Act
        var lists = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.CompanySource.PriceLists), TestContext.Current.CancellationToken));
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
        services.AddSingleton(Authorization([CompaniesPermissions.ManageCompanies.Name]));
        services.AddSingleton(SiteService());
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>));

        // Act
        new DataSourcesStartup().ConfigureServices(services);

        await using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredService<CompanyReportDataSource>();
        var dataSets = await source.GetDataSetsAsync(new DataSourceContext { User = _user }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(CommerceReportDataSets.CompanySource.Name, CompanyReportDataSource.Name);
        Assert.Equal(4, dataSets.Count);
    }

    private static async Task SaveAsync(ISession session, object item, CancellationToken cancellationToken)
    {
        await session.SaveAsync(item, false, CompaniesConstants.CollectionName, cancellationToken);
        await session.FlushAsync(cancellationToken);
    }

    private static CompanyReportDataSource Source(ISession session, string[] grants = null)
    {
        var authorizationService = Authorization(grants ?? [CompaniesPermissions.ManageCompanies.Name]);
        var siteService = SiteService();

        return new CompanyReportDataSource(
        [
            new CompaniesReportDataSet(session, authorizationService, siteService, new PassThroughStringLocalizer<CompaniesReportDataSet>()),
            new CompanyBuyersReportDataSet(session, authorizationService, siteService, new PassThroughStringLocalizer<CompanyBuyersReportDataSet>()),
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
