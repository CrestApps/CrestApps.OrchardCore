using System.Security.Claims;
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
/// The installment plan data sets of the subscriptions data source read real plans from a SQLite store, so the index
/// queries, the fields they expose, the push-down of dates, join keys and statuses, the grouping in the database, and
/// the permission they require are all exercised end to end.
/// </summary>
public sealed class InstallmentPlanReportDataSourceTests : IAsyncLifetime
{
    private static readonly ClaimsPrincipal _user = ReportDesignerPrincipals.User("u1", "Manager");
    private static readonly DateTime _start = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"installment-plan-reports-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_databasePath};Pooling=False"));
        _store.RegisterIndexes([new InstallmentPlanIndexProvider()], SubscriptionConstants.InstallmentPlanCollectionName);

        await _store.InitializeAsync(cancellationToken);
        await _store.InitializeCollectionAsync(SubscriptionConstants.InstallmentPlanCollectionName, cancellationToken);

        await using (var session = _store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(cancellationToken);
            var schemaBuilder = new SchemaBuilder(_store.Configuration, transaction);

            await new InstallmentPlanMigrations { SchemaBuilder = schemaBuilder }.CreateAsync();

            await transaction.CommitAsync(cancellationToken);
        }

        await using (var session = _store.CreateSession())
        {
            // Three plans, one a day: an active one with a down payment, a past due one replacing a transaction, and a
            // canceled one.
            for (var day = 0; day < 3; day++)
            {
                await session.SaveAsync(Plan(day), false, SubscriptionConstants.InstallmentPlanCollectionName, cancellationToken);
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
    public async Task WithoutManageInstallmentPlans_NothingIsExposed()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session, grants: [SubscriptionPermissions.ManageSubscriptions.Name]);
        var context = new DataSourceContext { User = _user };

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.SubscriptionSource.InstallmentPlans, context, TestContext.Current.CancellationToken);
        var plans = await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.SubscriptionSource.InstallmentPlans), TestContext.Current.CancellationToken);
        var installments = await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.SubscriptionSource.Installments), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(dataSets);
        Assert.Null(schema);
        Assert.Empty(plans.Rows);
        Assert.Empty(installments.Rows);
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
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.SubscriptionSource.Installments, context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(dataSets);
        Assert.Null(schema);
    }

    [Fact]
    public async Task Schema_InstallmentPlans_DeclaresKeysAndReferences_AndNeverExposesPrivateDetails()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var context = new DataSourceContext { User = _user };

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var plans = await source.GetSchemaAsync(CommerceReportDataSets.SubscriptionSource.InstallmentPlans, context, TestContext.Current.CancellationToken);
        var installments = await source.GetSchemaAsync(CommerceReportDataSets.SubscriptionSource.Installments, context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [
                CommerceReportDataSets.SubscriptionSource.InstallmentPlans,
                CommerceReportDataSets.SubscriptionSource.Installments,
            ],
            dataSets.Select(dataSet => dataSet.Name));
        Assert.Equal(InstallmentPlansReportDataSet.CreatedUtcField, plans.DataSet.DefaultDateField);
        Assert.True(plans.FindField(CommerceReportDataSets.ItemIdField).IsKeyFilterable);
        Assert.True(plans.FindField(InstallmentPlansReportDataSet.OwnerIdField).IsKeyFilterable);
        Assert.True(plans.FindField("SourceTransactionId").IsKeyFilterable);
        Assert.False(plans.FindField("ReferenceId").IsKeyFilterable);

        var user = Assert.Single(plans.FindField(InstallmentPlansReportDataSet.OwnerIdField).References);
        Assert.Equal(ReportsConstants.UsersDataSource, user.Source);

        var transaction = Assert.Single(plans.FindField("SourceTransactionId").References);
        Assert.Equal(CommerceReportDataSets.TransactionSource.Name, transaction.Source);
        Assert.Equal(CommerceReportDataSets.TransactionSource.Transactions, transaction.DataSet);
        Assert.Equal(CommerceReportDataSets.ItemIdField, transaction.Field);

        Assert.False(string.IsNullOrEmpty(plans.FindField("AmountOutstanding").Description));
        Assert.DoesNotContain(plans.Fields, field => field.Name is "Notes" or "PaymentMethod" or "DownPaymentSessionId" or "Events" or "Payments");

        Assert.Equal(InstallmentsReportDataSet.PlanCreatedUtcField, installments.DataSet.DefaultDateField);
        var plan = Assert.Single(installments.FindField(InstallmentsReportDataSet.PlanIdField).References);
        Assert.Equal(CommerceReportDataSets.SubscriptionSource.Name, plan.Source);
        Assert.Equal(CommerceReportDataSets.SubscriptionSource.InstallmentPlans, plan.DataSet);
        Assert.Single(installments.FindField("TransactionId").References);
        Assert.DoesNotContain(installments.Fields, field => field.Name is "LastFailureMessage" or "CheckoutSessionIds");
    }

    [Fact]
    public async Task Query_InstallmentPlans_ReturnsTypedValues_NewestFirst()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);

        // Act
        var rows = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.SubscriptionSource.InstallmentPlans), TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["plan-2", "plan-1", "plan-0"], rows.Select(row => row[CommerceReportDataSets.ItemIdField]));

        var active = rows[2];
        Assert.Equal("Season fee", active["Title"]);
        Assert.Equal("Active", active["Status"]);
        Assert.Equal("user-1", active[InstallmentPlansReportDataSet.OwnerIdField]);
        Assert.Equal("Ada", active["CustomerName"]);
        Assert.Equal("USD", active["Currency"]);
        Assert.Equal(300m, active["TotalAmount"]);
        Assert.Equal(100m, active["DownPaymentAmount"]);
        Assert.Equal(200m, active["AmountPaid"]);
        Assert.Equal(100m, active["AmountOutstanding"]);
        Assert.Equal(2L, active["InstallmentCount"]);
        Assert.Equal("Monthly", active["Frequency"]);
        Assert.Equal("AutoCharge", active["CollectionMethod"]);
        Assert.Equal(_start.AddDays(60), active["NextDueUtc"]);
        Assert.Equal("Registration", active["ReferenceType"]);

        var pastDue = rows[1];
        Assert.Equal("PastDue", pastDue["Status"]);
        Assert.Equal("tx-source", pastDue["SourceTransactionId"]);
        Assert.Equal(0m, pastDue["AmountPaid"]);

        var canceled = rows[0];
        Assert.Equal("Canceled", canceled["Status"]);
        Assert.Equal(0m, canceled["AmountOutstanding"]);
        Assert.Null(canceled["NextDueUtc"]);

        Assert.All(rows, row => Assert.DoesNotContain(row.Values, value => value is string text && text.Contains("Call before", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Query_InstallmentPlans_PushDownNeverDropsAMatchingRow()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var dates = Query(CommerceReportDataSets.SubscriptionSource.InstallmentPlans, CommerceReportDataSets.ItemIdField);
        dates.Conditions.Add(Condition(InstallmentPlansReportDataSet.CreatedUtcField, DataFilterOperator.Between, _start, _start.AddDays(1)));
        var owners = Query(CommerceReportDataSets.SubscriptionSource.InstallmentPlans, CommerceReportDataSets.ItemIdField);
        owners.Conditions.Add(JoinKeys(InstallmentPlansReportDataSet.OwnerIdField, "user-1"));
        var replaced = Query(CommerceReportDataSets.SubscriptionSource.InstallmentPlans, CommerceReportDataSets.ItemIdField);
        replaced.Conditions.Add(JoinKeys("SourceTransactionId", "tx-source"));
        var status = Query(CommerceReportDataSets.SubscriptionSource.InstallmentPlans, CommerceReportDataSets.ItemIdField);
        status.Conditions.Add(Condition("Status", DataFilterOperator.In, "active", "pastdue"));
        var none = Query(CommerceReportDataSets.SubscriptionSource.InstallmentPlans, CommerceReportDataSets.ItemIdField);
        none.Conditions.Add(JoinKeys(InstallmentPlansReportDataSet.OwnerIdField));

        // Act
        var dateRows = Rows(await ReportDesignerTestServices.ReadAsync(source, dates, TestContext.Current.CancellationToken));
        var ownerRows = Rows(await ReportDesignerTestServices.ReadAsync(source, owners, TestContext.Current.CancellationToken));
        var replacedRows = Rows(await ReportDesignerTestServices.ReadAsync(source, replaced, TestContext.Current.CancellationToken));
        var statusRows = Rows(await ReportDesignerTestServices.ReadAsync(source, status, TestContext.Current.CancellationToken));
        var noRows = Rows(await ReportDesignerTestServices.ReadAsync(source, none, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["plan-1", "plan-0"], dateRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal(["plan-2", "plan-0"], ownerRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal(["plan-1"], replacedRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal(["plan-1", "plan-0"], statusRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Empty(noRows);
    }

    [Fact]
    public async Task Aggregate_InstallmentPlans_CountsInTheDatabase_AndDeclinesCutTexts()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);

        // Act
        var byStatus = await source.AggregateAsync(Aggregate(CommerceReportDataSets.SubscriptionSource.InstallmentPlans, "Status"), TestContext.Current.CancellationToken);
        var byOwner = await source.AggregateAsync(Aggregate(CommerceReportDataSets.SubscriptionSource.InstallmentPlans, InstallmentPlansReportDataSet.OwnerIdField), TestContext.Current.CancellationToken);
        var byTitle = await source.AggregateAsync(Aggregate(CommerceReportDataSets.SubscriptionSource.InstallmentPlans, "Title"), TestContext.Current.CancellationToken);
        var byEmail = await source.AggregateAsync(Aggregate(CommerceReportDataSets.SubscriptionSource.InstallmentPlans, "CustomerEmail"), TestContext.Current.CancellationToken);
        var installments = await source.AggregateAsync(Aggregate(CommerceReportDataSets.SubscriptionSource.Installments, "PlanStatus"), TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(byStatus);
        var statuses = byStatus.ToDictionary(row => (string)row[0], row => (long)row[1]);
        Assert.Equal(1L, statuses["Active"]);
        Assert.Equal(1L, statuses["PastDue"]);
        Assert.Equal(1L, statuses["Canceled"]);

        Assert.NotNull(byOwner);
        var owners = byOwner.ToDictionary(row => (string)row[0], row => (long)row[1]);
        Assert.Equal(2L, owners["user-1"]);
        Assert.Equal(1L, owners["user-2"]);

        // The index cuts these texts, so the grouping is left to the report.
        Assert.Null(byTitle);
        Assert.Null(byEmail);
        Assert.Null(installments);
    }

    [Fact]
    public async Task Query_Installments_GivesOneRowPerPaymentWithItsPlan()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var query = Query(CommerceReportDataSets.SubscriptionSource.Installments);
        query.Conditions.Add(JoinKeys(InstallmentsReportDataSet.PlanIdField, "plan-0", "plan-1"));

        // Act
        var rows = Rows(await ReportDesignerTestServices.ReadAsync(source, query, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["plan-1", "plan-1", "plan-0", "plan-0", "plan-0"], rows.Select(row => row[InstallmentsReportDataSet.PlanIdField]));

        var downPayment = rows[2];
        Assert.Equal(0L, downPayment["Number"]);
        Assert.Equal(true, downPayment["IsDownPayment"]);
        Assert.Equal("Paid", downPayment["Status"]);
        Assert.Equal(100m, downPayment["Amount"]);
        Assert.Equal("tx-0-0", downPayment["TransactionId"]);
        Assert.Equal("USD", downPayment["Currency"]);
        Assert.Equal("Active", downPayment["PlanStatus"]);
        Assert.Equal("Season fee", downPayment["PlanTitle"]);
        Assert.Equal(DateTimeKind.Utc, Assert.IsType<DateTime>(downPayment["PaidUtc"]).Kind);

        Assert.Equal([0L, 1L, 2L], rows.Skip(2).Select(row => row["Number"]));
        Assert.Equal("Due", rows[4]["Status"]);

        var declined = rows[0];
        Assert.Equal(1L, declined["Number"]);
        Assert.Equal(false, declined["IsDownPayment"]);
        Assert.Equal("Failed", declined["Status"]);
        Assert.Equal(2L, declined["ChargeAttempts"]);
        Assert.Equal("EUR", declined["Currency"]);
        Assert.NotNull(declined["NextChargeAttemptUtc"]);
        Assert.All(rows, row => Assert.DoesNotContain(row.Values, value => value is string text && text.Contains("declined", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task InstallmentPlansDataSourcesStartup_RegistersTheDataSourceWithoutTheSubscriptionsFeature()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var alone = Services(session);
        var both = Services(session);

        // Act
        new InstallmentPlansDataSourcesStartup().ConfigureServices(alone);
        new DataSourcesStartup().ConfigureServices(both);
        new InstallmentPlansDataSourcesStartup().ConfigureServices(both);

        await using var aloneProvider = alone.BuildServiceProvider();
        await using var bothProvider = both.BuildServiceProvider();
        var aloneDataSets = await aloneProvider.GetRequiredService<SubscriptionReportDataSource>().GetDataSetsAsync(new DataSourceContext { User = _user }, TestContext.Current.CancellationToken);
        var bothDataSets = await bothProvider.GetRequiredService<SubscriptionReportDataSource>().GetDataSetsAsync(new DataSourceContext { User = _user }, TestContext.Current.CancellationToken);
        var options = bothProvider.GetRequiredService<IOptions<DataSourceOptions>>().Value;

        // Assert
        Assert.Equal(
            [
                CommerceReportDataSets.SubscriptionSource.InstallmentPlans,
                CommerceReportDataSets.SubscriptionSource.Installments,
            ],
            aloneDataSets.Select(dataSet => dataSet.Name));
        Assert.Equal(4, bothDataSets.Count);
        Assert.Single(options.DataSources, dataSource => dataSource.Type == typeof(SubscriptionReportDataSource));
    }

    private static InstallmentPlan Plan(int day)
    {
        var created = _start.AddDays(day);
        var plan = new InstallmentPlan
        {
            ItemId = $"plan-{day}",
            Title = day == 0 ? "Season fee" : "Camp fee",
            ReferenceType = day == 0 ? "Registration" : null,
            ReferenceId = day == 0 ? "registration-1" : null,
            SourceTransactionId = day == 1 ? "tx-source" : null,
            OwnerId = day == 1 ? "user-2" : "user-1",
            CustomerName = day == 1 ? "Grace" : "Ada",
            CustomerEmail = day == 1 ? "grace@example.com" : "ada@example.com",
            Currency = day == 1 ? "EUR" : "USD",
            TotalAmount = day == 1 ? 200m : 300m,
            DownPaymentAmount = day == 0 ? 100m : 0m,
            InstallmentCount = 2,
            Frequency = InstallmentFrequency.Monthly,
            FirstDueUtc = created.AddDays(30),
            CollectionMethod = InstallmentCollectionMethod.AutoCharge,
            Status = day switch
            {
                0 => InstallmentPlanStatus.Active,
                1 => InstallmentPlanStatus.PastDue,
                _ => InstallmentPlanStatus.Canceled,
            },
            Notes = "Call before charging the card.",
            CreatedById = "admin-1",
            CreatedByName = "Admin",
            CreatedUtc = created,
            UpdatedUtc = created,
            ActivatedUtc = day == 0 ? created : null,
            CanceledUtc = day == 2 ? created.AddDays(1) : null,
        };

        if (day == 0)
        {
            plan.Payments.Add(Payment(0, created, 100m, InstallmentPaymentStatus.Paid, "tx-0-0"));
            plan.Payments.Add(Payment(1, created.AddDays(30), 100m, InstallmentPaymentStatus.Paid, "tx-0-1"));
            plan.Payments.Add(Payment(2, created.AddDays(60), 100m, InstallmentPaymentStatus.Due, "tx-0-2"));
        }
        else if (day == 1)
        {
            var declined = Payment(1, created, 100m, InstallmentPaymentStatus.Failed, "tx-1-1");
            declined.ChargeAttempts = 2;
            declined.LastChargeAttemptUtc = created.AddDays(1);
            declined.NextChargeAttemptUtc = created.AddDays(3);
            declined.LastFailureMessage = "Card declined by the bank.";

            plan.Payments.Add(declined);
            plan.Payments.Add(Payment(2, created.AddDays(30), 100m, InstallmentPaymentStatus.Scheduled, "tx-1-2"));
        }
        else
        {
            plan.Payments.Add(Payment(1, created.AddDays(30), 150m, InstallmentPaymentStatus.Canceled, "tx-2-1"));
            plan.Payments.Add(Payment(2, created.AddDays(60), 150m, InstallmentPaymentStatus.Canceled, "tx-2-2"));
        }

        plan.Events.Add(new InstallmentPlanEvent { CreatedUtc = created, Type = InstallmentPlanEventType.Created, Message = "Created by Admin." });

        return plan;
    }

    private static InstallmentPlanPayment Payment(int number, DateTime dueUtc, decimal amount, InstallmentPaymentStatus status, string transactionId)
    {
        return new InstallmentPlanPayment
        {
            Number = number,
            DueUtc = dueUtc,
            Amount = amount,
            Status = status,
            TransactionId = transactionId,
            PaidUtc = status == InstallmentPaymentStatus.Paid ? dueUtc : null,
        };
    }

    private static ServiceCollection Services(ISession session)
    {
        var services = new ServiceCollection();

        services.AddSingleton(session);
        services.AddSingleton(Authorization([SubscriptionPermissions.ManageSubscriptions.Name, SubscriptionPermissions.ManageInstallmentPlans.Name]));
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>));

        return services;
    }

    private static SubscriptionReportDataSource Source(ISession session, string[] grants = null)
    {
        var authorizationService = Authorization(grants ?? [SubscriptionPermissions.ManageInstallmentPlans.Name]);

        return new SubscriptionReportDataSource(
        [
            new InstallmentPlansReportDataSet(session, authorizationService, new PassThroughStringLocalizer<InstallmentPlansReportDataSet>()),
            new InstallmentsReportDataSet(session, authorizationService, new PassThroughStringLocalizer<InstallmentsReportDataSet>()),
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

    private static DataAggregateQuery Aggregate(string dataSet, string group)
    {
        return new DataAggregateQuery
        {
            DataSet = dataSet,
            Groups = [new DataAggregateGroup { Field = group }],
            Measures = [new DataAggregateMeasure { Function = DataAggregate.Count }],
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
