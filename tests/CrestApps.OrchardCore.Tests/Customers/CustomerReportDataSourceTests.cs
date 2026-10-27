using System.Security.Claims;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Customers;
using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Customers.Core.Indexes;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Indexes;
using CrestApps.OrchardCore.Customers.Migrations;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.Reports.DataSources;
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
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Customers;

/// <summary>
/// The customers data source reads real customers, loyalty points, store credit and referral links from a SQLite
/// store, so the index queries, the fields they expose, the push-down of dates, join keys and statuses, the grouping in
/// the database, and the permissions they require are all exercised end to end.
/// </summary>
public sealed class CustomerReportDataSourceTests : IAsyncLifetime
{
    private static readonly ClaimsPrincipal _user = ReportDesignerPrincipals.User("u1", "Manager");
    private static readonly DateTime _start = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"customer-reports-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_databasePath};Pooling=False"));
        _store.RegisterIndexes(
            [
                new CustomerIndexProvider(),
                new LoyaltyAccountIndexProvider(),
                new StoreCreditAccountIndexProvider(),
                new ReferralProfileIndexProvider(),
            ],
            CustomersConstants.CollectionName);

        await _store.InitializeAsync(cancellationToken);
        await _store.InitializeCollectionAsync(CustomersConstants.CollectionName, cancellationToken);

        await using (var session = _store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(cancellationToken);
            var schemaBuilder = new SchemaBuilder(_store.Configuration, transaction);

            await new CustomerMigrations { SchemaBuilder = schemaBuilder }.CreateAsync();
            await new LoyaltyMigrations { SchemaBuilder = schemaBuilder }.CreateAsync();
            await new StoreCreditMigrations { SchemaBuilder = schemaBuilder }.CreateAsync();
            await new ReferralMigrations { SchemaBuilder = schemaBuilder }.CreateAsync();

            await transaction.CommitAsync(cancellationToken);
        }

        await using (var session = _store.CreateSession())
        {
            // Four customers, one a day: an anonymized one, a person with an account, a business guest, and a guest
            // merged into the account's customer. Each record is flushed on its own, so the index rows keep the order.
            for (var day = 0; day < 4; day++)
            {
                await SaveAsync(session, Customer(day), cancellationToken);
            }

            await SaveAsync(session, LoyaltyAccount("loyalty-1", "customer-1", _start.AddDays(1)), cancellationToken);
            await SaveAsync(session, LoyaltyAccount("loyalty-3", "customer-3", _start.AddDays(3)), cancellationToken);
            await SaveAsync(session, StoreCredit("credit-1", "USD", [30m, -5m], _start.AddDays(1)), cancellationToken);
            await SaveAsync(session, StoreCredit("credit-2", "EUR", [10m], _start.AddDays(2)), cancellationToken);
            await SaveAsync(session, Referral(), cancellationToken);

            await session.SaveChangesAsync(cancellationToken);
        }
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task WithoutViewCustomers_NothingIsExposed()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session, grants: []);
        var context = new DataSourceContext { User = _user };

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.CustomerSource.Customers, context, TestContext.Current.CancellationToken);
        var customers = await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.CustomerSource.Customers), TestContext.Current.CancellationToken);
        var activity = await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.CustomerSource.LoyaltyActivity), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(dataSets);
        Assert.Null(schema);
        Assert.Empty(customers.Rows);
        Assert.Empty(activity.Rows);
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
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.CustomerSource.Customers, context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(dataSets);
        Assert.Null(schema);
    }

    [Fact]
    public async Task ViewCustomersAlone_HidesTheReferrals()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session, grants: [CustomerPermissions.ViewCustomers.Name]);
        var context = new DataSourceContext { User = _user };

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.CustomerSource.Referrals, context, TestContext.Current.CancellationToken);
        var rewards = await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.CustomerSource.ReferralRewards), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [
                CommerceReportDataSets.CustomerSource.Customers,
                CommerceReportDataSets.CustomerSource.LoyaltyAccounts,
                CommerceReportDataSets.CustomerSource.LoyaltyActivity,
                CommerceReportDataSets.CustomerSource.StoreCreditAccounts,
                CommerceReportDataSets.CustomerSource.StoreCreditActivity,
            ],
            dataSets.Select(dataSet => dataSet.Name));
        Assert.Null(schema);
        Assert.Empty(rewards.Rows);
    }

    [Fact]
    public async Task Schema_Customers_DeclaresKeysAndReferences_AndNeverExposesPrivateDetails()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var context = new DataSourceContext { User = _user };

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.CustomerSource.Customers, context, TestContext.Current.CancellationToken);
        var referrals = await source.GetSchemaAsync(CommerceReportDataSets.CustomerSource.Referrals, context, TestContext.Current.CancellationToken);
        var rewards = await source.GetSchemaAsync(CommerceReportDataSets.CustomerSource.ReferralRewards, context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [
                CommerceReportDataSets.CustomerSource.Customers,
                CommerceReportDataSets.CustomerSource.LoyaltyAccounts,
                CommerceReportDataSets.CustomerSource.LoyaltyActivity,
                CommerceReportDataSets.CustomerSource.StoreCreditAccounts,
                CommerceReportDataSets.CustomerSource.StoreCreditActivity,
                CommerceReportDataSets.CustomerSource.Referrals,
                CommerceReportDataSets.CustomerSource.ReferralRewards,
            ],
            dataSets.Select(dataSet => dataSet.Name));
        Assert.Equal(CustomersReportDataSet.CreatedUtcField, schema.DataSet.DefaultDateField);
        Assert.True(schema.FindField(CommerceReportDataSets.ItemIdField).IsIdentifier);
        Assert.True(schema.FindField(CommerceReportDataSets.ItemIdField).IsKeyFilterable);
        Assert.True(schema.FindField("UserId").IsKeyFilterable);

        var user = Assert.Single(schema.FindField("UserId").References);
        Assert.Equal(ReportsConstants.UsersDataSource, user.Source);
        Assert.Equal(ReportsConstants.UserIdField, user.Field);

        var merged = Assert.Single(schema.FindField("MergedIntoCustomerId").References);
        Assert.Equal(CommerceReportDataSets.CustomerSource.Name, merged.Source);
        Assert.Equal(CommerceReportDataSets.CustomerSource.Customers, merged.DataSet);
        Assert.Equal(CommerceReportDataSets.ItemIdField, merged.Field);

        Assert.Equal(DataFieldType.Boolean, schema.FindField("AcceptsMarketing").Type);
        Assert.DoesNotContain(schema.Fields, field => field.Name is "TaxId" or "TaxExemptionReason" or "ExternalIds" or "PrivacyLog" or "Addresses" or "BillingAddressLine1" or "AddressLine1" or "NormalizedEmail");
        Assert.DoesNotContain(referrals.Fields, field => field.Name is "Code");
        Assert.DoesNotContain(rewards.Fields, field => field.Name is "Code" or "FriendEmail" or "FriendName");
        Assert.Null(referrals.DataSet.DefaultDateField);
    }

    [Fact]
    public async Task Query_Customers_ReturnsTypedValues_NewestFirst()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);

        // Act
        var rows = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.CustomerSource.Customers), TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["customer-3", "customer-2", "customer-1", "customer-0"], rows.Select(row => row[CommerceReportDataSets.ItemIdField]));

        var person = rows[2];
        Assert.Equal("Ada Lovelace", person["Name"]);
        Assert.Equal("Ada", person["FirstName"]);
        Assert.Equal("Person", person["Kind"]);
        Assert.Equal("Active", person["Status"]);
        Assert.Equal(true, person["HasAccount"]);
        Assert.Equal("user-1", person["UserId"]);
        Assert.Equal("ada@example.com", person["Email"]);
        Assert.Equal("+15550000001", person["Phone"]);
        Assert.Equal(true, person["AcceptsMarketing"]);
        Assert.Equal(true, person["IsTaxExempt"]);
        Assert.Equal(2L, person["AddressCount"]);
        Assert.Equal("DE", person["BillingCountry"]);
        Assert.Equal("Berlin", person["BillingCity"]);
        Assert.Equal("FR", person["ShippingCountry"]);
        Assert.Equal(1L, person["MergedCustomerCount"]);
        Assert.Equal(false, person["IsAnonymized"]);
        Assert.Equal(_start.AddDays(1), person[CustomersReportDataSet.CreatedUtcField]);
        Assert.Equal(DateTimeKind.Utc, Assert.IsType<DateTime>(person["LastActivityUtc"]).Kind);

        var business = rows[1];
        Assert.Equal("Fabrikam", business["Name"]);
        Assert.Equal("Business", business["Kind"]);
        Assert.Equal(false, business["HasAccount"]);
        Assert.Null(business["UserId"]);
        Assert.Null(business["BillingCountry"]);

        var merged = rows[0];
        Assert.Equal("Merged", merged["Status"]);
        Assert.Equal("customer-1", merged["MergedIntoCustomerId"]);

        var anonymized = rows[3];
        Assert.Equal("Anonymized", anonymized["Status"]);
        Assert.Equal(true, anonymized["IsAnonymized"]);
        Assert.Null(anonymized["Name"]);
        Assert.Null(anonymized["Email"]);
        Assert.Null(anonymized["Phone"]);
        Assert.Null(anonymized["UserId"]);
        Assert.Equal(false, anonymized["AcceptsMarketing"]);
        Assert.Equal(0L, anonymized["AddressCount"]);

        Assert.DoesNotContain(rows.SelectMany(row => row.Values), value => value is string text && (text.Contains("SECRET", StringComparison.Ordinal) || text.Contains("cus_", StringComparison.Ordinal) || text.Contains("Private", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Query_Customers_PushDownNeverDropsAMatchingRow()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var dates = Query(CommerceReportDataSets.CustomerSource.Customers, CommerceReportDataSets.ItemIdField);
        dates.Conditions.Add(Condition(CustomersReportDataSet.CreatedUtcField, DataFilterOperator.Between, _start.AddDays(1), _start.AddDays(2)));
        var keys = Query(CommerceReportDataSets.CustomerSource.Customers, CommerceReportDataSets.ItemIdField);
        keys.Conditions.Add(new DataCondition { Field = "UserId", Operator = DataFilterOperator.In, Values = ["user-1"], IsJoinKey = true });
        var status = Query(CommerceReportDataSets.CustomerSource.Customers, CommerceReportDataSets.ItemIdField);
        status.Conditions.Add(Condition("Status", DataFilterOperator.In, "merged", "anonymized"));
        var kind = Query(CommerceReportDataSets.CustomerSource.Customers, CommerceReportDataSets.ItemIdField);
        kind.Conditions.Add(Condition("Kind", DataFilterOperator.Equals, "business"));
        var none = Query(CommerceReportDataSets.CustomerSource.Customers, CommerceReportDataSets.ItemIdField);
        none.Conditions.Add(new DataCondition { Field = CommerceReportDataSets.ItemIdField, Operator = DataFilterOperator.In, Values = [], IsJoinKey = true });

        // Act
        var dateRows = Rows(await ReportDesignerTestServices.ReadAsync(source, dates, TestContext.Current.CancellationToken));
        var keyRows = Rows(await ReportDesignerTestServices.ReadAsync(source, keys, TestContext.Current.CancellationToken));
        var statusRows = Rows(await ReportDesignerTestServices.ReadAsync(source, status, TestContext.Current.CancellationToken));
        var kindRows = Rows(await ReportDesignerTestServices.ReadAsync(source, kind, TestContext.Current.CancellationToken));
        var noRows = Rows(await ReportDesignerTestServices.ReadAsync(source, none, TestContext.Current.CancellationToken));

        // Assert
        // The bounds are inclusive, so the rows on them stay; the report applies the strict comparison itself.
        Assert.Equal(["customer-2", "customer-1"], dateRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal(["customer-1"], keyRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal(["customer-3", "customer-0"], statusRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal(["customer-2"], kindRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Empty(noRows);
    }

    [Fact]
    public async Task Query_Customers_StopsAtMaxRows()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var query = Query(CommerceReportDataSets.CustomerSource.Customers, CommerceReportDataSets.ItemIdField);
        query.MaxRows = 2;

        // Act
        var table = await ReportDesignerTestServices.ReadAsync(source, query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["customer-3", "customer-2"], Rows(table).Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.True(table.Truncated);
    }

    [Fact]
    public async Task Aggregate_Customers_CountsByStatusInTheDatabase()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var query = new DataAggregateQuery
        {
            DataSet = CommerceReportDataSets.CustomerSource.Customers,
            Groups = [new DataAggregateGroup { Field = "Status" }],
            Measures = [new DataAggregateMeasure { Function = DataAggregate.Count }],
            Context = new DataSourceContext { User = _user },
        };

        // Act
        var groups = await ((IDataAggregateSource)source).AggregateAsync(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(groups);
        var counts = groups.ToDictionary(row => (string)row[0], row => (long)row[1]);
        Assert.Equal(2L, counts["Active"]);
        Assert.Equal(1L, counts["Anonymized"]);
        Assert.Equal(1L, counts["Merged"]);
    }

    [Fact]
    public async Task Aggregate_Customers_DeclinesTheEmail()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var query = new DataAggregateQuery
        {
            DataSet = CommerceReportDataSets.CustomerSource.Customers,
            Groups = [new DataAggregateGroup { Field = "Email" }],
            Measures = [new DataAggregateMeasure { Function = DataAggregate.Count }],
            Context = new DataSourceContext { User = _user },
        };

        // Act
        var groups = await ((IDataAggregateSource)source).AggregateAsync(query, TestContext.Current.CancellationToken);

        // Assert
        // The index keeps the email in upper case, so only the row-by-row read groups it.
        Assert.Null(groups);
    }

    [Fact]
    public async Task Query_LoyaltyAccountsAndActivity_ComeFromTheAccounts()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var activity = Query(CommerceReportDataSets.CustomerSource.LoyaltyActivity);
        activity.Conditions.Add(new DataCondition { Field = LoyaltyActivityReportDataSet.LoyaltyAccountIdField, Operator = DataFilterOperator.In, Values = ["loyalty-1"], IsJoinKey = true });

        // Act
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.CustomerSource.LoyaltyActivity, new DataSourceContext { User = _user }, TestContext.Current.CancellationToken);
        var accounts = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.CustomerSource.LoyaltyAccounts), TestContext.Current.CancellationToken));
        var changes = Rows(await ReportDesignerTestServices.ReadAsync(source, activity, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["loyalty-3", "loyalty-1"], accounts.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal("customer-1", accounts[1]["CustomerId"]);
        Assert.Equal(150L, accounts[1]["Points"]);
        Assert.Equal(200L, accounts[1]["EarnedPoints"]);
        Assert.Equal(50L, accounts[1]["SpentPoints"]);
        Assert.Equal(2L, accounts[1]["ActivityCount"]);

        Assert.True(schema.FindField(LoyaltyActivityReportDataSet.LoyaltyAccountIdField).IsKeyFilterable);
        Assert.True(schema.FindField("CustomerId").IsKeyFilterable);
        Assert.Equal(CommerceReportDataSets.OrderSource.Name, Assert.Single(schema.FindField("OrderId").References).Source);
        Assert.Equal(CommerceReportDataSets.CheckoutSource.CheckoutSessions, Assert.Single(schema.FindField("CheckoutSessionId").References).DataSet);

        Assert.Equal([1L, 2L], changes.Select(row => row["Position"]));
        Assert.All(changes, row => Assert.Equal("loyalty-1", row[LoyaltyActivityReportDataSet.LoyaltyAccountIdField]));
        Assert.Equal(["Earned", "Spent"], changes.Select(row => row["Kind"]));
        Assert.Equal([200L, -50L], changes.Select(row => row["Points"]));
        Assert.Equal([200L, 150L], changes.Select(row => row["Balance"]));
        Assert.Equal("order-1", changes[0]["OrderId"]);
        Assert.Equal("session-1", changes[1]["CheckoutSessionId"]);
        Assert.Equal("Order N-1", changes[0]["Reason"]);
    }

    [Fact]
    public async Task Aggregate_LoyaltyActivity_Declines()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var query = new DataAggregateQuery
        {
            DataSet = CommerceReportDataSets.CustomerSource.LoyaltyActivity,
            Groups = [new DataAggregateGroup { Field = "Kind" }],
            Measures = [new DataAggregateMeasure { Function = DataAggregate.Count }],
            Context = new DataSourceContext { User = _user },
        };

        // Act
        var groups = await ((IDataAggregateSource)source).AggregateAsync(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(groups);
    }

    [Fact]
    public async Task Query_StoreCredit_GivesMoneyWithItsCurrency()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var context = new DataSourceContext { User = _user };
        var activity = Query(CommerceReportDataSets.CustomerSource.StoreCreditActivity);
        activity.Conditions.Add(new DataCondition { Field = "CustomerId", Operator = DataFilterOperator.In, Values = ["customer-1"], IsJoinKey = true });
        var none = Query(CommerceReportDataSets.CustomerSource.StoreCreditActivity);
        none.Conditions.Add(new DataCondition { Field = StoreCreditActivityReportDataSet.StoreCreditAccountIdField, Operator = DataFilterOperator.In, Values = [], IsJoinKey = true });

        // Act
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.CustomerSource.StoreCreditAccounts, context, TestContext.Current.CancellationToken);
        var accounts = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.CustomerSource.StoreCreditAccounts), TestContext.Current.CancellationToken));
        var changes = Rows(await ReportDesignerTestServices.ReadAsync(source, activity, TestContext.Current.CancellationToken));
        var noRows = Rows(await ReportDesignerTestServices.ReadAsync(source, none, TestContext.Current.CancellationToken));

        // Assert
        Assert.Null(schema.DataSet.DefaultDateField);
        Assert.False(string.IsNullOrEmpty(schema.FindField("Balance").Description));
        Assert.Equal(DataFieldType.Text, schema.FindField("Currency").Type);

        Assert.Equal(["credit-2", "credit-1"], accounts.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal("USD", accounts[1]["Currency"]);
        Assert.Equal(25m, accounts[1]["Balance"]);
        Assert.Equal(30m, accounts[1]["GivenAmount"]);
        Assert.Equal(5m, accounts[1]["SpentAmount"]);

        // Accounts come newest first, and each one gives its changes oldest first.
        Assert.Equal(["credit-2", "credit-1", "credit-1"], changes.Select(row => row[StoreCreditActivityReportDataSet.StoreCreditAccountIdField]));
        Assert.Equal(["EUR", "USD", "USD"], changes.Select(row => row["Currency"]));
        Assert.Equal([10m, 30m, -5m], changes.Select(row => row["Amount"]));
        Assert.Equal("session-2", changes[2]["CheckoutSessionId"]);
        Assert.Empty(noRows);
    }

    [Fact]
    public async Task Aggregate_StoreCreditAccounts_SumsTheBalanceOfACurrencyInTheDatabase_AndLeavesAGroupByCurrencyToTheReport()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var byCurrency = new DataAggregateQuery
        {
            DataSet = CommerceReportDataSets.CustomerSource.StoreCreditAccounts,
            Groups = [new DataAggregateGroup { Field = "Currency" }],
            Measures = [new DataAggregateMeasure { Function = DataAggregate.Sum, Field = "Balance" }],
            Context = new DataSourceContext { User = _user },
        };

        async Task<decimal> BalanceAsync(string currency)
        {
            var groups = await ((IDataAggregateSource)source).AggregateAsync(new DataAggregateQuery
            {
                DataSet = CommerceReportDataSets.CustomerSource.StoreCreditAccounts,
                Measures = [new DataAggregateMeasure { Function = DataAggregate.Sum, Field = "Balance" }],
                Conditions = [new DataCondition { Field = "Currency", Operator = DataFilterOperator.Equals, Values = [currency] }],
                Context = new DataSourceContext { User = _user },
            }, TestContext.Current.CancellationToken);

            Assert.NotNull(groups);

            return Convert.ToDecimal(Assert.Single(groups)[0]);
        }

        // Act
        var declined = await ((IDataAggregateSource)source).AggregateAsync(byCurrency, TestContext.Current.CancellationToken);

        // Assert
        // The database groups text by its own collation, so a group by a text column is left to the report.
        Assert.Null(declined);
        Assert.Equal(25m, await BalanceAsync("USD"));
        Assert.Equal(10m, await BalanceAsync("eur"));
    }

    [Fact]
    public async Task Query_Referrals_NeverExposeTheCodeOrTheFriends()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);

        // Act
        var referrals = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.CustomerSource.Referrals), TestContext.Current.CancellationToken));
        var rewards = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.CustomerSource.ReferralRewards), TestContext.Current.CancellationToken));

        // Assert
        var referral = Assert.Single(referrals);
        Assert.Equal("customer-1", referral["CustomerId"]);
        Assert.Equal(2L, referral["ReferralCount"]);
        Assert.Equal(500L, referral["RewardPoints"]);

        Assert.Equal(["order-9", "order-10"], rewards.Select(row => row["OrderId"]));
        Assert.Equal(["StoreCredit", "Points"], rewards.Select(row => row["RewardKind"]));
        Assert.Equal("USD", rewards[0]["Currency"]);
        Assert.Equal(10m, rewards[0]["RewardAmount"]);
        Assert.Equal(500L, rewards[1]["RewardPoints"]);
        Assert.All(rewards, row => Assert.Equal("referral-1", row[ReferralRewardsReportDataSet.ReferralIdField]));
        Assert.DoesNotContain(referrals.Concat(rewards).SelectMany(row => row.Values), value => value is string text && (text.Contains("ADA", StringComparison.Ordinal) || text.Contains("Bob", StringComparison.Ordinal) || text.Contains('@')));
    }

    [Fact]
    public async Task ReportIndexMigration_CanRunAgain()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(_store.Configuration, transaction);

        // Act
        var version = await new CustomerMigrations { SchemaBuilder = schemaBuilder }.UpdateFrom1Async();
        var again = await new CustomerMigrations { SchemaBuilder = schemaBuilder }.UpdateFrom1Async();

        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, version);
        Assert.Equal(2, again);
    }

    [Fact]
    public async Task DataSourcesStartups_RegisterTheDataSourceAndTheFeatureDataSets()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var services = new ServiceCollection();

        services.AddSingleton(session);
        services.AddSingleton(Authorization([CustomerPermissions.ViewCustomers.Name, CustomerPermissions.ManageCustomers.Name]));
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>));

        // Act
        new DataSourcesStartup().ConfigureServices(services);
        new StoreCreditDataSourcesStartup().ConfigureServices(services);
        new LoyaltyDataSourcesStartup().ConfigureServices(services);

        await using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredService<CustomerReportDataSource>();
        var dataSets = await source.GetDataSetsAsync(new DataSourceContext { User = _user }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(CommerceReportDataSets.CustomerSource.Name, CustomerReportDataSource.Name);
        Assert.Contains(dataSets, dataSet => dataSet.Name == CommerceReportDataSets.CustomerSource.StoreCreditActivity);
        Assert.Contains(dataSets, dataSet => dataSet.Name == CommerceReportDataSets.CustomerSource.LoyaltyAccounts);
        Assert.DoesNotContain(dataSets, dataSet => dataSet.Name == CommerceReportDataSets.CustomerSource.Referrals);
        Assert.Equal(5, dataSets.Count);
    }

    private static Customer Customer(int day)
    {
        var created = _start.AddDays(day);

        var customer = new Customer
        {
            ItemId = $"customer-{day}",
            Kind = day == 2 ? CustomerKind.Business : CustomerKind.Person,
            Status = day == 3 ? CustomerStatus.Merged : CustomerStatus.Active,
            UserId = day is 0 or 1 ? $"user-{day}" : null,
            FirstName = day == 2 ? null : "Ada",
            LastName = day == 2 ? null : "Lovelace",
            CompanyName = day == 2 ? "Fabrikam" : null,
            Email = $"ada{day}@example.com",
            Phone = "+1555000000" + day,
            AcceptsMarketing = day is 0 or 1,
            IsTaxExempt = day == 1,
            TaxId = "SECRET-TAX",
            TaxExemptionReason = "SECRET-CERTIFICATE",
            ExternalIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["stripe"] = $"cus_{day}" },
            CreatedUtc = created,
            ModifiedUtc = created,
            LastActivityUtc = created.AddHours(1),
        };

        if (day is 0 or 1)
        {
            customer.Addresses =
            [
                new CustomerAddress { AddressId = "home", Address = new Address { Country = "DE", Region = "BE", City = "Berlin", PostalCode = "10115", AddressLine1 = "1 Private Street" } },
                new CustomerAddress { AddressId = "office", Address = new Address { Country = "FR", City = "Paris", AddressLine1 = "2 Private Avenue" } },
            ];
            customer.DefaultBillingAddressId = "home";
            customer.DefaultShippingAddressId = "office";
        }

        if (day == 1)
        {
            customer.Email = "ada@example.com";
            customer.MergedCustomerIds = ["customer-3"];
        }

        if (day == 3)
        {
            customer.MergedIntoCustomerId = "customer-1";
            customer.MergedUtc = created.AddHours(2);
        }

        if (day == 0)
        {
            customer.Anonymize(created.AddDays(30));
        }

        return customer;
    }

    private static LoyaltyAccount LoyaltyAccount(string itemId, string customerId, DateTime created)
    {
        var account = new LoyaltyAccount
        {
            ItemId = itemId,
            CustomerId = customerId,
            CreatedUtc = created,
        };

        account.ChangePoints(200, LoyaltyActivityKind.Earned, "Order N-1", created, orderId: "order-1");
        account.ChangePoints(-50, LoyaltyActivityKind.Spent, "Checkout", created.AddDays(1), sessionId: "session-1");
        account.UpdatedUtc = created.AddDays(1);

        return account;
    }

    private static StoreCreditAccount StoreCredit(string itemId, string currency, decimal[] changes, DateTime created)
    {
        var account = new StoreCreditAccount
        {
            ItemId = itemId,
            CustomerId = "customer-1",
            Currency = currency,
            CreatedUtc = created,
        };

        for (var index = 0; index < changes.Length; index++)
        {
            account.ChangeBalance(changes[index], changes[index] > 0m ? "Refund" : "Checkout", created.AddHours(index), sessionId: changes[index] > 0m ? null : "session-2", actor: "staff");
        }

        account.UpdatedUtc = created.AddHours(changes.Length);

        return account;
    }

    private static ReferralProfile Referral()
    {
        return new ReferralProfile
        {
            ItemId = "referral-1",
            CustomerId = "customer-1",
            Code = "ADA123",
            Referrals =
            [
                new ReferralRecord { FriendEmail = "bob@example.com", FriendName = "Bob", OrderId = "order-9", OrderNumber = "N-9", Reward = "10 store credit", RewardKind = ReferralRewardKind.StoreCredit, RewardAmount = 10m, RewardCurrency = "USD", CreatedUtc = _start.AddDays(5) },
                new ReferralRecord { FriendEmail = "bob.two@example.com", FriendName = "Bob Two", OrderId = "order-10", OrderNumber = "N-10", Reward = "500 points", RewardKind = ReferralRewardKind.Points, RewardPoints = 500, CreatedUtc = _start.AddDays(6) },
            ],
            CreatedUtc = _start.AddDays(4),
            UpdatedUtc = _start.AddDays(6),
        };
    }

    private static async Task SaveAsync(ISession session, object item, CancellationToken cancellationToken)
    {
        await session.SaveAsync(item, false, CustomersConstants.CollectionName, cancellationToken);
        await session.FlushAsync(cancellationToken);
    }

    private static CustomerReportDataSource Source(ISession session, string[] grants = null)
    {
        var authorizationService = Authorization(grants ?? [CustomerPermissions.ViewCustomers.Name, CustomerPermissions.ManageCustomers.Name]);

        return new CustomerReportDataSource(
        [
            new CustomersReportDataSet(session, authorizationService, new PassThroughStringLocalizer<CustomersReportDataSet>()),
            new LoyaltyAccountsReportDataSet(session, authorizationService, new PassThroughStringLocalizer<LoyaltyAccountsReportDataSet>()),
            new LoyaltyActivityReportDataSet(session, authorizationService, new PassThroughStringLocalizer<LoyaltyActivityReportDataSet>()),
            new StoreCreditAccountsReportDataSet(session, authorizationService, new PassThroughStringLocalizer<StoreCreditAccountsReportDataSet>()),
            new StoreCreditActivityReportDataSet(session, authorizationService, new PassThroughStringLocalizer<StoreCreditActivityReportDataSet>()),
            new ReferralsReportDataSet(session, authorizationService, new PassThroughStringLocalizer<ReferralsReportDataSet>()),
            new ReferralRewardsReportDataSet(session, authorizationService, new PassThroughStringLocalizer<ReferralRewardsReportDataSet>()),
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
