using System.Globalization;
using System.Security.Claims;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Subscriptions;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using CrestApps.OrchardCore.Subscriptions.Migrations;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Reports.DataSources;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.DataSources;
using OrchardCore.Security;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Subscriptions.Reports;

/// <summary>
/// The subscriptions data source reads real subscription agreements from a SQLite store, so the index queries, the
/// fields they expose, the push-down of dates, join keys and statuses, the grouping in the database, and the
/// permission they require are all exercised end to end.
/// </summary>
public sealed class SubscriptionReportDataSourceTests : IAsyncLifetime
{
    private static readonly ClaimsPrincipal _user = ReportDesignerPrincipals.User("u1", "Manager");
    private static readonly DateTime _start = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"subscription-reports-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_databasePath};Pooling=False"));
        _store.RegisterIndexes([new SubscriptionRecordIndexProvider()], SubscriptionConstants.SubscriptionCollectionName);
        _store.RegisterIndexes([new InstallmentPlanIndexProvider()], SubscriptionConstants.InstallmentPlanCollectionName);

        await _store.InitializeAsync(cancellationToken);
        await _store.InitializeCollectionAsync(SubscriptionConstants.SubscriptionCollectionName, cancellationToken);
        await _store.InitializeCollectionAsync(SubscriptionConstants.InstallmentPlanCollectionName, cancellationToken);

        await using (var session = _store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(cancellationToken);
            var schemaBuilder = new SchemaBuilder(_store.Configuration, transaction);

            Assert.Equal(2, await new SubscriptionRecordMigrations { SchemaBuilder = schemaBuilder }.CreateAsync());
            Assert.Equal(2, await new InstallmentPlanMigrations { SchemaBuilder = schemaBuilder }.CreateAsync());

            await transaction.CommitAsync(cancellationToken);
        }

        await using (var session = _store.CreateSession())
        {
            // Three subscriptions, one a day: an active one of user-1, a guest's past due one, and a canceled one.
            for (var day = 0; day < 3; day++)
            {
                await session.SaveAsync(Subscription(day), false, SubscriptionConstants.SubscriptionCollectionName, cancellationToken);
            }

            await session.SaveChangesAsync(cancellationToken);
        }
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task WithoutManageSubscriptions_NothingIsExposed()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session, grants: [SubscriptionPermissions.ManageOwnSubscriptions.Name]);
        var context = new DataSourceContext { User = _user };

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.SubscriptionSource.Subscriptions, context, TestContext.Current.CancellationToken);
        var table = await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.SubscriptionSource.Subscriptions), TestContext.Current.CancellationToken);
        var lines = await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.SubscriptionSource.SubscriptionLines), TestContext.Current.CancellationToken);

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
        var query = Query(CommerceReportDataSets.SubscriptionSource.Subscriptions);
        query.Context = context;

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.SubscriptionSource.Subscriptions, context, TestContext.Current.CancellationToken);
        var table = await ReportDesignerTestServices.ReadAsync(source, query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(dataSets);
        Assert.Null(schema);
        Assert.Empty(table.Rows);
    }

    [Fact]
    public async Task Schema_Subscriptions_DeclaresKeysAndReferences_AndNeverExposesPrivateDetails()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var context = new DataSourceContext { User = _user };

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.SubscriptionSource.Subscriptions, context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [
                CommerceReportDataSets.SubscriptionSource.Subscriptions,
                CommerceReportDataSets.SubscriptionSource.SubscriptionLines,
            ],
            dataSets.Select(dataSet => dataSet.Name));
        Assert.Equal(SubscriptionsReportDataSet.CreatedUtcField, schema.DataSet.DefaultDateField);
        Assert.True(schema.FindField(CommerceReportDataSets.ItemIdField).IsIdentifier);
        Assert.True(schema.FindField(CommerceReportDataSets.ItemIdField).IsKeyFilterable);
        Assert.True(schema.FindField(SubscriptionsReportDataSet.OwnerIdField).IsKeyFilterable);
        Assert.True(schema.FindField("ReferenceId").IsKeyFilterable);
        Assert.True(schema.FindField("CheckoutSessionId").IsKeyFilterable);

        var owners = schema.FindField(SubscriptionsReportDataSet.OwnerIdField).References;
        Assert.Equal(2, owners.Count);
        Assert.Equal(ReportsConstants.UsersDataSource, owners[0].Source);
        Assert.Equal(ReportsConstants.UserIdField, owners[0].Field);
        Assert.Equal(CommerceReportDataSets.CustomerSource.Name, owners[1].Source);
        Assert.Equal(CommerceReportDataSets.CustomerSource.Customers, owners[1].DataSet);
        Assert.Equal(ReportsConstants.UserIdField, owners[1].Field);

        var plan = Assert.Single(schema.FindField("ReferenceId").References);
        Assert.Equal(CommerceReportDataSets.ProductSource.Name, plan.Source);
        Assert.Equal(CommerceReportDataSets.ProductSource.Products, plan.DataSet);
        Assert.Equal(CommerceReportDataSets.ContentItemIdField, plan.Field);

        var checkout = Assert.Single(schema.FindField("CheckoutSessionId").References);
        Assert.Equal(CommerceReportDataSets.CheckoutSource.Name, checkout.Source);
        Assert.Equal(CommerceReportDataSets.CheckoutSource.CheckoutSessions, checkout.DataSet);

        Assert.Equal(DataFieldType.Decimal, schema.FindField("TotalAmount").Type);
        Assert.False(string.IsNullOrEmpty(schema.FindField("TotalAmount").Description));
        Assert.Equal(DataFieldType.Text, schema.FindField("Currency").Type);
        Assert.DoesNotContain(schema.Fields, field => field.Name is "ProviderCustomerId" or "ObligationId" or "Events" or "Entitlements" or "GuestContactName");
    }

    [Fact]
    public async Task Query_Subscriptions_ReturnsTypedValues_NewestFirst()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);

        // Act
        var rows = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.SubscriptionSource.Subscriptions), TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["sub-2", "sub-1", "sub-0"], rows.Select(row => row[CommerceReportDataSets.ItemIdField]));

        var active = rows[2];
        Assert.Equal("Gold", active["Title"]);
        Assert.Equal("Active", active["Status"]);
        Assert.Equal("plan-gold", active["ReferenceId"]);
        Assert.Equal(SubscriptionCheckout.ReferenceType, active["ReferenceType"]);
        Assert.Equal("user-1", active[SubscriptionsReportDataSet.OwnerIdField]);
        Assert.Equal(false, active["IsGuest"]);
        Assert.Equal("USD", active["Currency"]);
        Assert.Equal(30m, active["Amount"]);
        Assert.Equal(3m, active["TaxAmount"]);
        Assert.Equal(33m, active["TotalAmount"]);
        Assert.Equal(1L, active["BillingDuration"]);
        Assert.Equal("Month", active["DurationType"]);
        Assert.Equal(3L, active["CyclesBilled"]);
        Assert.Null(active["BillingCycleLimit"]);
        Assert.Equal(2L, active["LineCount"]);
        Assert.Equal("Live", active["GatewayMode"]);
        Assert.Equal(DateTimeKind.Utc, Assert.IsType<DateTime>(active["NextBillingUtc"]).Kind);
        Assert.Null(active["CanceledUtc"]);

        var guest = rows[1];
        Assert.Equal("PastDue", guest["Status"]);
        Assert.Equal(true, guest["IsGuest"]);
        Assert.Equal("guest@example.com", guest["GuestContactEmail"]);
        Assert.Equal("EUR", guest["Currency"]);
        Assert.NotNull(guest["PastDueSinceUtc"]);

        var canceled = rows[0];
        Assert.Equal("Canceled", canceled["Status"]);
        Assert.Equal("Too expensive", canceled["CancellationReason"]);
        Assert.Equal(true, canceled["CancelAtPeriodEnd"]);
        Assert.Null(canceled["NextBillingUtc"]);

        Assert.All(rows, row => Assert.DoesNotContain(row.Values, value => value is string text && text.StartsWith("cus_", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Query_Subscriptions_PushDownNeverDropsAMatchingRow()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var dates = Query(CommerceReportDataSets.SubscriptionSource.Subscriptions, CommerceReportDataSets.ItemIdField);
        dates.Conditions.Add(Condition(SubscriptionsReportDataSet.CreatedUtcField, DataFilterOperator.Between, _start.AddDays(1), _start.AddDays(2)));
        var owners = Query(CommerceReportDataSets.SubscriptionSource.Subscriptions, CommerceReportDataSets.ItemIdField);
        owners.Conditions.Add(JoinKeys(SubscriptionsReportDataSet.OwnerIdField, "user-1"));
        var plans = Query(CommerceReportDataSets.SubscriptionSource.Subscriptions, CommerceReportDataSets.ItemIdField);
        plans.Conditions.Add(JoinKeys("ReferenceId", "plan-silver"));
        var status = Query(CommerceReportDataSets.SubscriptionSource.Subscriptions, CommerceReportDataSets.ItemIdField);
        status.Conditions.Add(Condition("Status", DataFilterOperator.In, "pastdue", "canceled"));
        var currency = Query(CommerceReportDataSets.SubscriptionSource.Subscriptions, CommerceReportDataSets.ItemIdField);
        currency.Conditions.Add(Condition("Currency", DataFilterOperator.Equals, "usd"));
        var none = Query(CommerceReportDataSets.SubscriptionSource.Subscriptions, CommerceReportDataSets.ItemIdField);
        none.Conditions.Add(JoinKeys(CommerceReportDataSets.ItemIdField));

        // Act
        var dateRows = Rows(await ReportDesignerTestServices.ReadAsync(source, dates, TestContext.Current.CancellationToken));
        var ownerRows = Rows(await ReportDesignerTestServices.ReadAsync(source, owners, TestContext.Current.CancellationToken));
        var planRows = Rows(await ReportDesignerTestServices.ReadAsync(source, plans, TestContext.Current.CancellationToken));
        var statusRows = Rows(await ReportDesignerTestServices.ReadAsync(source, status, TestContext.Current.CancellationToken));
        var currencyRows = Rows(await ReportDesignerTestServices.ReadAsync(source, currency, TestContext.Current.CancellationToken));
        var noRows = Rows(await ReportDesignerTestServices.ReadAsync(source, none, TestContext.Current.CancellationToken));

        // Assert
        // The bounds are inclusive, so the rows on them stay; the report applies the strict comparison itself.
        Assert.Equal(["sub-2", "sub-1"], dateRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal(["sub-0"], ownerRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal(["sub-2", "sub-1"], planRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal(["sub-2", "sub-1"], statusRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal(["sub-2", "sub-0"], currencyRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Empty(noRows);
    }

    [Fact]
    public async Task Aggregate_Subscriptions_CountsAndSumsInTheDatabase()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var byStatus = Aggregate(CommerceReportDataSets.SubscriptionSource.Subscriptions, "Status", new DataAggregateMeasure { Function = DataAggregate.Count });
        var byCurrency = Aggregate(CommerceReportDataSets.SubscriptionSource.Subscriptions, "Currency", new DataAggregateMeasure { Function = DataAggregate.Sum, Field = "TotalAmount" });

        // Act
        var statuses = await ((IDataAggregateSource)source).AggregateAsync(byStatus, TestContext.Current.CancellationToken);
        var currencies = await ((IDataAggregateSource)source).AggregateAsync(byCurrency, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(statuses);
        var counts = statuses.ToDictionary(row => (string)row[0], row => (long)row[1]);
        Assert.Equal(1L, counts["Active"]);
        Assert.Equal(1L, counts["PastDue"]);
        Assert.Equal(1L, counts["Canceled"]);

        // The index keeps the currency as text, which the database groups by its own collation, so the grouping is left
        // to the report; a filter on it is still applied in the database.
        Assert.Null(currencies);

        foreach (var (currency, total) in new[] { ("USD", 66m), ("eur", 33m) })
        {
            var query = Aggregate(CommerceReportDataSets.SubscriptionSource.Subscriptions, "Status", new DataAggregateMeasure { Function = DataAggregate.Sum, Field = "TotalAmount" });
            query.Groups = [];
            query.Conditions = [new DataCondition { Field = "Currency", Operator = DataFilterOperator.Equals, Values = [currency] }];

            var groups = await ((IDataAggregateSource)source).AggregateAsync(query, TestContext.Current.CancellationToken);

            Assert.NotNull(groups);
            Assert.Equal(total, Convert.ToDecimal(Assert.Single(groups)[0], System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    [Fact]
    public async Task Query_SubscriptionLines_GivesOneRowPerLineWithItsSubscription()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var query = Query(CommerceReportDataSets.SubscriptionSource.SubscriptionLines);
        query.Conditions.Add(JoinKeys(SubscriptionLinesReportDataSet.SubscriptionIdField, "sub-0"));

        // Act
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.SubscriptionSource.SubscriptionLines, new DataSourceContext { User = _user }, TestContext.Current.CancellationToken);
        var rows = Rows(await ReportDesignerTestServices.ReadAsync(source, query, TestContext.Current.CancellationToken));
        var all = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.SubscriptionSource.SubscriptionLines), TestContext.Current.CancellationToken));
        var groups = await ((IDataAggregateSource)source).AggregateAsync(
            Aggregate(CommerceReportDataSets.SubscriptionSource.SubscriptionLines, "SubscriptionStatus", new DataAggregateMeasure { Function = DataAggregate.Count }),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SubscriptionLinesReportDataSet.SubscriptionCreatedUtcField, schema.DataSet.DefaultDateField);
        Assert.True(schema.FindField(SubscriptionLinesReportDataSet.SubscriptionIdField).IsKeyFilterable);
        var subscription = Assert.Single(schema.FindField(SubscriptionLinesReportDataSet.SubscriptionIdField).References);
        Assert.Equal(CommerceReportDataSets.SubscriptionSource.Name, subscription.Source);
        Assert.Equal(CommerceReportDataSets.SubscriptionSource.Subscriptions, subscription.DataSet);

        Assert.Equal(["line-0a", "line-0b"], rows.Select(row => row["LineId"]));
        Assert.Equal([1L, 2L], rows.Select(row => row["Position"]));
        Assert.All(rows, row => Assert.Equal("sub-0", row[SubscriptionLinesReportDataSet.SubscriptionIdField]));
        Assert.All(rows, row => Assert.Equal("Active", row["SubscriptionStatus"]));
        Assert.All(rows, row => Assert.Equal("USD", row["Currency"]));
        Assert.Equal(2L, rows[1]["Quantity"]);
        Assert.Equal(5m, rows[1]["UnitPrice"]);
        Assert.Equal(10m, rows[1]["LineTotal"]);
        Assert.Equal("Extra seats", rows[1]["Description"]);

        Assert.Equal(6, all.Count);
        Assert.Equal(["sub-2", "sub-2", "sub-1", "sub-1", "sub-0", "sub-0"], all.Select(row => row[SubscriptionLinesReportDataSet.SubscriptionIdField]));
        Assert.Null(groups);
    }

    [Fact]
    public async Task RecipeReports_RunOverTheSubscriptionsDataSource()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var designs = ReportRecipeDesigns.Load("CrestApps.OrchardCore.Subscriptions", "subscriptions-report-builder-reports.recipe.json");

        // Act
        var results = await ReportRecipeDesigns.RunAsync(designs, _user, _start.AddDays(10), new TestDataSourceEntry(SubscriptionReportDataSource.Name, Source(session)));

        // Assert
        Assert.Equal(["subscriptions-new", "subscriptions-expiring"], designs.Select(design => design.ItemId));
        Assert.All(designs, design => Assert.Equal("Store", design.Category));

        // The three subscriptions started in the same month.
        var started = Assert.Single(results["subscriptions-new"].Rows);
        Assert.Equal(3m, Convert.ToDecimal(started[results["subscriptions-new"].IndexOf("subscriptions")], CultureInfo.InvariantCulture));

        // The active and the past due subscriptions are paid through a month after they started, 20 and 21 days from now.
        // The canceled one is left out.
        var expiring = results["subscriptions-expiring"];
        Assert.Equal(2, expiring.Rows.Count);
        Assert.Equal("user-1", ReportRecipeDesigns.Value(expiring, 0, "subscriber"));
        Assert.Equal(20m, Convert.ToDecimal(ReportRecipeDesigns.Value(expiring, 0, "daysleft"), CultureInfo.InvariantCulture));
        Assert.Equal("session-9", ReportRecipeDesigns.Value(expiring, 1, "subscriber"));
    }

    [Fact]
    public async Task ReportIndexMigrations_CanRunAgain()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(_store.Configuration, transaction);

        // Act
        var subscriptions = await new SubscriptionRecordMigrations { SchemaBuilder = schemaBuilder }.UpdateFrom1Async();
        var plans = await new InstallmentPlanMigrations { SchemaBuilder = schemaBuilder }.UpdateFrom1Async();

        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, subscriptions);
        Assert.Equal(2, plans);
    }

    [Fact]
    public async Task DataSourcesStartup_RegistersTheDataSourceAndTheSubscriptionDataSets()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var services = new ServiceCollection();

        services.AddSingleton(session);
        services.AddSingleton(Authorization([SubscriptionPermissions.ManageSubscriptions.Name, SubscriptionPermissions.ManageInstallmentPlans.Name]));
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>));

        // Act
        new DataSourcesStartup().ConfigureServices(services);

        await using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredService<SubscriptionReportDataSource>();
        var dataSets = await source.GetDataSetsAsync(new DataSourceContext { User = _user }, TestContext.Current.CancellationToken);
        var options = provider.GetRequiredService<IOptions<DataSourceOptions>>().Value;

        // Assert
        Assert.Equal(CommerceReportDataSets.SubscriptionSource.Name, SubscriptionReportDataSource.Name);
        Assert.Single(options.DataSources, dataSource => dataSource.Type == typeof(SubscriptionReportDataSource));
        Assert.Equal(
            [
                CommerceReportDataSets.SubscriptionSource.Subscriptions,
                CommerceReportDataSets.SubscriptionSource.SubscriptionLines,
            ],
            dataSets.Select(dataSet => dataSet.Name));
    }

    private static Subscription Subscription(int day)
    {
        var started = _start.AddDays(day);

        var subscription = new Subscription
        {
            ItemId = $"sub-{day}",
            Title = day == 0 ? "Gold" : "Silver",
            OwnerId = day switch
            {
                0 => "user-1",
                1 => "session-9",
                _ => "user-2",
            },
            OwnerKind = day == 1 ? CustomerOwnerKind.Guest : CustomerOwnerKind.Authenticated,
            GuestContactName = day == 1 ? "Grace" : null,
            GuestContactEmail = day == 1 ? "guest@example.com" : null,
            ReferenceType = SubscriptionCheckout.ReferenceType,
            ReferenceId = day == 0 ? "plan-gold" : "plan-silver",
            ReferenceVersionId = $"plan-version-{day}",
            CheckoutSessionId = $"checkout-{day}",
            ObligationId = $"obligation-{day}",
            ProviderKey = "Stripe",
            ProviderSubscriptionId = $"sub_gateway_{day}",
            ProviderCustomerId = $"cus_secret_{day}",
            GatewayMode = GatewayMode.Live,
            Status = day switch
            {
                0 => SubscriptionStatus.Active,
                1 => SubscriptionStatus.PastDue,
                _ => SubscriptionStatus.Canceled,
            },
            Currency = day == 1 ? "EUR" : "USD",
            Amount = 30m,
            TaxAmount = 3m,
            BillingDuration = 1,
            DurationType = DurationType.Month,
            CyclesBilled = 3,
            CurrentPeriodStartUtc = started,
            CurrentPeriodEndUtc = started.AddMonths(1),
            NextBillingUtc = day == 2 ? null : started.AddMonths(1),
            PastDueSinceUtc = day == 1 ? started.AddDays(1) : null,
            GraceEndsUtc = day == 1 ? started.AddDays(8) : null,
            CancelAtPeriodEnd = day == 2,
            CanceledUtc = day == 2 ? started.AddDays(1) : null,
            CancellationReason = day == 2 ? "Too expensive" : null,
            CreatedUtc = started,
            UpdatedUtc = started,
            Lines =
            [
                new SubscriptionLine { ItemId = $"line-{day}a", Description = "Plan", Quantity = 1, UnitPrice = 20m },
                new SubscriptionLine { ItemId = $"line-{day}b", Description = "Extra seats", Quantity = 2, UnitPrice = 5m },
            ],
            Events =
            [
                new SubscriptionEvent { CreatedUtc = started, Type = SubscriptionEventType.Created, Message = "Created from checkout session." },
            ],
        };

        return subscription;
    }

    private static SubscriptionReportDataSource Source(ISession session, string[] grants = null)
    {
        var authorizationService = Authorization(grants ?? [SubscriptionPermissions.ManageSubscriptions.Name]);

        return new SubscriptionReportDataSource(
        [
            new SubscriptionsReportDataSet(session, authorizationService, new PassThroughStringLocalizer<SubscriptionsReportDataSet>()),
            new SubscriptionLinesReportDataSet(session, authorizationService, new PassThroughStringLocalizer<SubscriptionLinesReportDataSet>()),
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

    private static DataAggregateQuery Aggregate(string dataSet, string group, DataAggregateMeasure measure)
    {
        return new DataAggregateQuery
        {
            DataSet = dataSet,
            Groups = [new DataAggregateGroup { Field = group }],
            Measures = [measure],
            Context = new DataSourceContext { User = _user },
        };
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

    private static DataCondition JoinKeys(string field, params object[] keys)
    {
        return new DataCondition
        {
            Field = field,
            Operator = DataFilterOperator.In,
            Values = keys,
            IsJoinKey = true,
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
