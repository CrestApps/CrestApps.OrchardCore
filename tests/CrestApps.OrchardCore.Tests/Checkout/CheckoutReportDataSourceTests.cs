using System.Security.Claims;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core;
using CrestApps.OrchardCore.Checkout.Core.Indexes;
using CrestApps.OrchardCore.Checkout.Indexes;
using CrestApps.OrchardCore.Checkout.Migrations;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Reports.DataSources;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module;
using CrestApps.OrchardCore.Tests.Utilities;
using CrestApps.OrchardCore.Transactions.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.DataSources;
using OrchardCore.Entities;
using OrchardCore.Security;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;
using ISession = YesSql.ISession;
using OrderPermissions = CrestApps.OrchardCore.Orders.OrderPermissions;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// The checkout data source reads real checkout sessions, coupons, coupon uses and kept payment methods from a SQLite
/// store, so the index queries, the fields they expose, the push-down of dates, join keys and statuses, the grouping in
/// the database, and the permissions they require are all exercised end to end.
/// </summary>
public sealed class CheckoutReportDataSourceTests : IAsyncLifetime
{
    private static readonly ClaimsPrincipal _user = ReportDesignerPrincipals.User("u1", "Manager");
    private static readonly DateTime _start = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    private static readonly string[] _allGrants =
    [
        CheckoutReportPermissions.ManageOrders.Name,
        CheckoutPermissions.ManageCoupons.Name,
        TransactionsPermissions.ManageRefunds.Name,
    ];

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"checkout-reports-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_databasePath};Pooling=False"));
        _store.RegisterIndexes([new CheckoutSessionIndexProvider(), new CustomerPaymentMethodIndexProvider()]);
        _store.RegisterIndexes([new CouponIndexProvider(), new CouponRedemptionIndexProvider()], CheckoutConstants.CouponCollectionName);

        await _store.InitializeAsync(cancellationToken);
        await _store.InitializeCollectionAsync(CheckoutConstants.CouponCollectionName, cancellationToken);

        await using (var session = _store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(cancellationToken);
            var schemaBuilder = new SchemaBuilder(_store.Configuration, transaction);

            await new CheckoutMigrations { SchemaBuilder = schemaBuilder }.CreateAsync();
            await new CouponMigrations { SchemaBuilder = schemaBuilder }.CreateAsync();

            await transaction.CommitAsync(cancellationToken);
        }

        await using (var session = _store.CreateSession())
        {
            // A paid order checkout, a guest's that expired, one that settles a transaction and has no invoice yet, and
            // an anonymized one, a day apart.
            for (var day = 0; day < 4; day++)
            {
                await session.SaveAsync(CheckoutSession(day), false, null, cancellationToken);
            }

            await session.SaveAsync(new Coupon
            {
                ItemId = "coupon-1",
                Code = "SAVE5",
                Description = "Five off",
                Kind = CouponKind.FixedAmount,
                Amount = 5m,
                Currency = "USD",
                MinimumAmount = 20m,
                MaxRedemptions = 100,
                RedemptionCount = 2,
                CustomerId = "user-9",
                CustomerEmail = "vip@example.com",
                CreatedUtc = _start,
                UpdatedUtc = _start,
            }, false, CheckoutConstants.CouponCollectionName, cancellationToken);

            await session.SaveAsync(new Coupon
            {
                ItemId = "coupon-2",
                Code = "AUTO10",
                Method = CouponMethod.Automatic,
                Kind = CouponKind.Percentage,
                Percentage = 10m,
                IsEnabled = false,
                CreatedUtc = _start.AddDays(1),
                UpdatedUtc = _start.AddDays(1),
            }, false, CheckoutConstants.CouponCollectionName, cancellationToken);

            await session.SaveAsync(new CouponRedemption
            {
                ItemId = "use-1",
                Code = "SAVE5",
                SessionId = "session-0",
                ReferenceType = CheckoutReferenceTypes.Order,
                ReferenceId = "order-0",
                CustomerId = "user-1",
                CustomerEmail = "ada@example.com",
                Status = CouponRedemptionStatus.Redeemed,
                IsCounted = true,
                Amount = 5m,
                Currency = "USD",
                CreatedUtc = _start,
                RedeemedUtc = _start.AddMinutes(5),
            }, false, CheckoutConstants.CouponCollectionName, cancellationToken);

            await session.SaveAsync(new CouponRedemption
            {
                ItemId = "use-2",
                Code = "SAVE5",
                SessionId = "session-1",
                ReferenceType = CheckoutReferenceTypes.Order,
                ReferenceId = "order-1",
                CustomerEmail = "guest@example.com",
                Status = CouponRedemptionStatus.Held,
                Amount = 5m,
                Currency = "USD",
                CreatedUtc = _start.AddDays(1),
                ExpiresUtc = _start.AddDays(1).AddMinutes(30),
            }, false, CheckoutConstants.CouponCollectionName, cancellationToken);

            await session.SaveAsync(new CustomerPaymentMethod
            {
                ItemId = "method-1",
                OwnerId = "user-1",
                PaymentMethod = new SavedPaymentMethod
                {
                    ProviderKey = "Stripe",
                    CustomerReference = "cus_secret",
                    PaymentMethodReference = "pm_secret",
                    Brand = "visa",
                    Last4 = "4242",
                    ExpirationMonth = 12,
                    ExpirationYear = 2030,
                },
                IsDefault = true,
                SourceSessionId = "session-0",
                CreatedUtc = _start,
                UpdatedUtc = _start,
            }, false, null, cancellationToken);

            await session.SaveAsync(new CustomerPaymentMethod
            {
                ItemId = "method-2",
                OwnerId = "user-2",
                PaymentMethod = new SavedPaymentMethod
                {
                    ProviderKey = "Stripe",
                    PaymentMethodReference = "pm_other_secret",
                    Brand = "mastercard",
                    Last4 = "5454",
                    ExpirationMonth = 1,
                    ExpirationYear = 2028,
                },
                CreatedUtc = _start.AddDays(1),
                UpdatedUtc = _start.AddDays(1),
            }, false, null, cancellationToken);

            await session.SaveChangesAsync(cancellationToken);
        }
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task WithoutPermissions_NothingIsExposed()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session, grants: []);
        var context = new DataSourceContext { User = _user };

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.CheckoutSource.CheckoutSessions, context, TestContext.Current.CancellationToken);
        var sessions = await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.CheckoutSource.CheckoutSessions), TestContext.Current.CancellationToken);
        var coupons = await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.CheckoutSource.Coupons), TestContext.Current.CancellationToken);
        var methods = await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.CheckoutSource.SavedPaymentMethods), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(dataSets);
        Assert.Null(schema);
        Assert.Empty(sessions.Rows);
        Assert.Empty(coupons.Rows);
        Assert.Empty(methods.Rows);
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
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.CheckoutSource.CheckoutSessions, context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(dataSets);
        Assert.Null(schema);
    }

    [Fact]
    public async Task EachDataSet_NeedsThePermissionOfItsScreens()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var context = new DataSourceContext { User = _user };

        // Act
        var orders = await Source(session, [OrderPermissions.ManageOrders.Name]).GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var coupons = await Source(session, [CheckoutPermissions.ManageCoupons.Name]).GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var refunds = await Source(session, [TransactionsPermissions.ManageRefunds.Name]).GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var sessionSchema = await Source(session, [CheckoutPermissions.ManageCoupons.Name]).GetSchemaAsync(CommerceReportDataSets.CheckoutSource.CheckoutSessions, context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(OrderPermissions.ManageOrders.Name, CheckoutReportPermissions.ManageOrders.Name);
        Assert.Equal([CommerceReportDataSets.CheckoutSource.CheckoutSessions], orders.Select(dataSet => dataSet.Name));
        Assert.Equal([CommerceReportDataSets.CheckoutSource.Coupons, CommerceReportDataSets.CheckoutSource.CouponRedemptions], coupons.Select(dataSet => dataSet.Name));
        Assert.Equal([CommerceReportDataSets.CheckoutSource.SavedPaymentMethods], refunds.Select(dataSet => dataSet.Name));
        Assert.Null(sessionSchema);
    }

    [Fact]
    public async Task Schema_CheckoutSessions_IsWhatOrdersJoinTo_AndNeverExposesPrivateDetails()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var context = new DataSourceContext { User = _user };
        var fromOrders = CommerceReportDataSets.Reference(CommerceReportDataSets.CheckoutSource.Name, CommerceReportDataSets.CheckoutSource.CheckoutSessions);

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.CheckoutSource.CheckoutSessions, context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [
                CommerceReportDataSets.CheckoutSource.CheckoutSessions,
                CommerceReportDataSets.CheckoutSource.Coupons,
                CommerceReportDataSets.CheckoutSource.CouponRedemptions,
                CommerceReportDataSets.CheckoutSource.SavedPaymentMethods,
            ],
            dataSets.Select(dataSet => dataSet.Name));
        Assert.Equal(CheckoutSessionsReportDataSet.CreatedUtcField, schema.DataSet.DefaultDateField);
        Assert.True(schema.FindField(fromOrders.Field).IsIdentifier);
        Assert.True(schema.FindField(fromOrders.Field).IsKeyFilterable);
        Assert.True(schema.FindField(CheckoutSessionsReportDataSet.UserIdField).IsKeyFilterable);

        var user = Assert.Single(schema.FindField(CheckoutSessionsReportDataSet.UserIdField).References);
        Assert.Equal(ReportsConstants.UsersDataSource, user.Source);
        Assert.Equal(ReportsConstants.UserIdField, user.Field);

        var order = Assert.Single(schema.FindField("OrderId").References);
        Assert.Equal(CommerceReportDataSets.OrderSource.Name, order.Source);
        Assert.Equal(CommerceReportDataSets.OrderSource.Orders, order.DataSet);

        Assert.Equal(DataFieldType.Decimal, schema.FindField("Total").Type);
        Assert.False(string.IsNullOrEmpty(schema.FindField("Total").Description));
        Assert.DoesNotContain(schema.Fields, field => field.Name is "IPAddress" or "AgentInfo" or "GuestTokenHash" or "SavedSteps" or "OwnerId" or "CustomerEmail" or "CustomerName" or "Email" or "Phone" or "BillingCity" or "BillingAddressLine1" or "Payments");
    }

    [Fact]
    public async Task Query_CheckoutSessions_ReturnsTypedValues_NewestFirst()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);

        // Act
        var rows = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.CheckoutSource.CheckoutSessions), TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["session-3", "session-2", "session-1", "session-0"], rows.Select(row => row[CommerceReportDataSets.ItemIdField]));

        var paid = rows[3];
        Assert.Equal("Completed", paid["Status"]);
        Assert.Equal("Order", paid["ReferenceType"]);
        Assert.Equal("order-0", paid["OrderId"]);
        Assert.Equal("USD", paid["Currency"]);
        Assert.Equal(110m, paid["Total"]);
        Assert.Equal(10m, paid["TaxAmount"]);
        Assert.Equal(5m, paid["DiscountTotal"]);
        Assert.Equal(20m, paid["TenderedAmount"]);
        Assert.Equal(90m, paid["AmountDue"]);
        Assert.Equal(2L, paid["LineCount"]);
        Assert.Equal(3L, paid["ItemCount"]);
        Assert.Equal("user-1", paid["UserId"]);
        Assert.Equal("DE", paid["BillingCountry"]);
        Assert.Equal("BE", paid["BillingRegion"]);
        Assert.Equal(DateTimeKind.Utc, Assert.IsType<DateTime>(paid["CompletedUtc"]).Kind);
        Assert.Null(paid["ExpiredUtc"]);
        Assert.DoesNotContain(paid.Values, value => value is string text && (text.Contains("Private", StringComparison.Ordinal) || text.Contains("203.0.113", StringComparison.Ordinal) || text.Contains('@')));

        var expired = rows[2];
        Assert.Equal("Expired", expired["Status"]);
        Assert.Equal("Payment", expired["CurrentStep"]);
        Assert.Equal(_start.AddDays(1).AddHours(24), expired["ExpiredUtc"]);
        Assert.Null(expired["UserId"]);
        Assert.Null(expired["CompletedUtc"]);

        var settling = rows[1];
        Assert.Equal("Transaction", settling["ReferenceType"]);
        Assert.Null(settling["OrderId"]);
        Assert.Equal(true, settling["SettlesExistingBalance"]);
        Assert.Null(settling["Total"]);
        Assert.Null(settling["LineCount"]);

        var anonymized = rows[0];
        Assert.Equal(true, anonymized["IsAnonymized"]);
        Assert.Null(anonymized["UserId"]);
        Assert.Equal("DE", anonymized["BillingCountry"]);
    }

    [Fact]
    public async Task Query_CheckoutSessions_PushDownNeverDropsAMatchingRow()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var dates = Query(CommerceReportDataSets.CheckoutSource.CheckoutSessions, CommerceReportDataSets.ItemIdField);
        dates.Conditions.Add(Condition(CheckoutSessionsReportDataSet.CreatedUtcField, DataFilterOperator.Between, _start.AddDays(1), _start.AddDays(2)));
        var keys = Query(CommerceReportDataSets.CheckoutSource.CheckoutSessions, CommerceReportDataSets.ItemIdField);
        keys.Conditions.Add(new DataCondition { Field = CommerceReportDataSets.ItemIdField, Operator = DataFilterOperator.In, Values = ["session-1", "session-2"], IsJoinKey = true });
        var users = Query(CommerceReportDataSets.CheckoutSource.CheckoutSessions, CommerceReportDataSets.ItemIdField);
        users.Conditions.Add(new DataCondition { Field = CheckoutSessionsReportDataSet.UserIdField, Operator = DataFilterOperator.In, Values = ["user-1"], IsJoinKey = true });
        var abandoned = Query(CommerceReportDataSets.CheckoutSource.CheckoutSessions, CommerceReportDataSets.ItemIdField);
        abandoned.Conditions.Add(Condition("Status", DataFilterOperator.In, "expired", "Canceled"));
        var orders = Query(CommerceReportDataSets.CheckoutSource.CheckoutSessions, CommerceReportDataSets.ItemIdField);
        orders.Conditions.Add(Condition("ReferenceType", DataFilterOperator.Equals, "order"));
        var none = Query(CommerceReportDataSets.CheckoutSource.CheckoutSessions, CommerceReportDataSets.ItemIdField);
        none.Conditions.Add(new DataCondition { Field = CommerceReportDataSets.ItemIdField, Operator = DataFilterOperator.In, Values = [], IsJoinKey = true });

        // Act
        var dateRows = Rows(await ReportDesignerTestServices.ReadAsync(source, dates, TestContext.Current.CancellationToken));
        var keyRows = Rows(await ReportDesignerTestServices.ReadAsync(source, keys, TestContext.Current.CancellationToken));
        var userRows = Rows(await ReportDesignerTestServices.ReadAsync(source, users, TestContext.Current.CancellationToken));
        var abandonedRows = Rows(await ReportDesignerTestServices.ReadAsync(source, abandoned, TestContext.Current.CancellationToken));
        var orderRows = Rows(await ReportDesignerTestServices.ReadAsync(source, orders, TestContext.Current.CancellationToken));
        var noRows = Rows(await ReportDesignerTestServices.ReadAsync(source, none, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["session-2", "session-1"], dateRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal(["session-2", "session-1"], keyRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal(["session-0"], userRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal(["session-1"], abandonedRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal(["session-3", "session-1", "session-0"], orderRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Empty(noRows);
    }

    [Fact]
    public async Task Aggregate_CheckoutSessions_CountsByStatusInTheDatabase()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var query = new DataAggregateQuery
        {
            DataSet = CommerceReportDataSets.CheckoutSource.CheckoutSessions,
            Groups = [new DataAggregateGroup { Field = "Status" }],
            Measures = [new DataAggregateMeasure { Function = DataAggregate.Count }],
            Context = new DataSourceContext { User = _user },
        };

        var currency = new DataAggregateQuery
        {
            DataSet = CommerceReportDataSets.CheckoutSource.CheckoutSessions,
            Groups = [new DataAggregateGroup { Field = "Currency" }],
            Measures = [new DataAggregateMeasure { Function = DataAggregate.Count }],
            Context = new DataSourceContext { User = _user },
        };

        var unindexed = new DataAggregateQuery
        {
            DataSet = CommerceReportDataSets.CheckoutSource.CheckoutSessions,
            Groups = [new DataAggregateGroup { Field = "Status" }],
            Measures = [new DataAggregateMeasure { Function = DataAggregate.Count }],
            Context = new DataSourceContext { User = _user },
        };

        unindexed.Conditions.Add(Condition("Currency", DataFilterOperator.Equals, "USD"));

        // Act
        var groups = await ((IDataAggregateSource)source).AggregateAsync(query, TestContext.Current.CancellationToken);
        var declined = await ((IDataAggregateSource)source).AggregateAsync(currency, TestContext.Current.CancellationToken);
        var filtered = await ((IDataAggregateSource)source).AggregateAsync(unindexed, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(groups);
        var counts = groups.ToDictionary(row => (string)row[0], row => (long)row[1]);
        Assert.Equal(2L, counts["Completed"]);
        Assert.Equal(1L, counts["Expired"]);
        Assert.Equal(1L, counts["Pending"]);
        Assert.Null(declined);

        // A condition on a field the index doesn't hold can't be answered in the database.
        Assert.Null(filtered);
    }

    [Fact]
    public async Task Query_CouponsAndTheirUses_ReturnTypedValues()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var context = new DataSourceContext { User = _user };
        var held = Query(CommerceReportDataSets.CheckoutSource.CouponRedemptions, CommerceReportDataSets.ItemIdField);
        held.Conditions.Add(Condition("Status", DataFilterOperator.In, "held"));
        var bySession = Query(CommerceReportDataSets.CheckoutSource.CouponRedemptions, CommerceReportDataSets.ItemIdField);
        bySession.Conditions.Add(new DataCondition { Field = "SessionId", Operator = DataFilterOperator.In, Values = ["session-0"], IsJoinKey = true });

        // Act
        var couponSchema = await source.GetSchemaAsync(CommerceReportDataSets.CheckoutSource.Coupons, context, TestContext.Current.CancellationToken);
        var coupons = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.CheckoutSource.Coupons), TestContext.Current.CancellationToken));
        var uses = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.CheckoutSource.CouponRedemptions), TestContext.Current.CancellationToken));
        var heldRows = Rows(await ReportDesignerTestServices.ReadAsync(source, held, TestContext.Current.CancellationToken));
        var sessionRows = Rows(await ReportDesignerTestServices.ReadAsync(source, bySession, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(CouponsReportDataSet.CreatedUtcField, couponSchema.DataSet.DefaultDateField);
        Assert.DoesNotContain(couponSchema.Fields, field => field.Name is "CustomerId" or "CustomerEmail" or "ProductIds" or "CategoryIds" or "Sources");

        Assert.Equal(["coupon-2", "coupon-1"], coupons.Select(row => row[CommerceReportDataSets.ItemIdField]));
        var fixedAmount = coupons[1];
        Assert.Equal("SAVE5", fixedAmount["Code"]);
        Assert.Equal("FixedAmount", fixedAmount["Kind"]);
        Assert.Equal(5m, fixedAmount["Amount"]);
        Assert.Equal("USD", fixedAmount["Currency"]);
        Assert.Equal(20m, fixedAmount["MinimumAmount"]);
        Assert.Equal(2L, fixedAmount["RedemptionCount"]);
        Assert.Equal(100L, fixedAmount["MaxRedemptions"]);
        Assert.Equal(true, fixedAmount["IsLimited"]);
        Assert.Equal(false, fixedAmount["IsAutomatic"]);
        Assert.DoesNotContain(fixedAmount.Values, value => value is string text && text.Contains("vip@", StringComparison.Ordinal));
        Assert.Equal(true, coupons[0]["IsAutomatic"]);
        Assert.Equal(false, coupons[0]["IsEnabled"]);

        Assert.Equal(["use-2", "use-1"], uses.Select(row => row[CommerceReportDataSets.ItemIdField]));
        var used = uses[1];
        Assert.Equal("Redeemed", used["Status"]);
        Assert.Equal("order-0", used["OrderId"]);
        Assert.Equal("user-1", used["UserId"]);
        Assert.Equal("session-0", used["SessionId"]);
        Assert.Equal(5m, used["Amount"]);
        Assert.Equal(true, used["IsCounted"]);
        Assert.Null(uses[0]["UserId"]);

        Assert.Equal(["use-2"], heldRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal(["use-1"], sessionRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
    }

    [Fact]
    public async Task Aggregate_CouponUses_CountsByStatusInTheDatabase()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var query = new DataAggregateQuery
        {
            DataSet = CommerceReportDataSets.CheckoutSource.CouponRedemptions,
            Groups = [new DataAggregateGroup { Field = "Status" }],
            Measures = [new DataAggregateMeasure { Function = DataAggregate.Count }],
            Context = new DataSourceContext { User = _user },
        };

        // Act
        var groups = await ((IDataAggregateSource)source).AggregateAsync(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(groups);
        var counts = groups.ToDictionary(row => (string)row[0], row => (long)row[1]);
        Assert.Equal(1L, counts["Redeemed"]);
        Assert.Equal(1L, counts["Held"]);
    }

    [Fact]
    public async Task Query_SavedPaymentMethods_NeverExposeTheCard()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var context = new DataSourceContext { User = _user };
        var byUser = Query(CommerceReportDataSets.CheckoutSource.SavedPaymentMethods, CommerceReportDataSets.ItemIdField);
        byUser.Conditions.Add(new DataCondition { Field = SavedPaymentMethodsReportDataSet.UserIdField, Operator = DataFilterOperator.In, Values = ["user-2"], IsJoinKey = true });

        // Act
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.CheckoutSource.SavedPaymentMethods, context, TestContext.Current.CancellationToken);
        var rows = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.CheckoutSource.SavedPaymentMethods), TestContext.Current.CancellationToken));
        var userRows = Rows(await ReportDesignerTestServices.ReadAsync(source, byUser, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(SavedPaymentMethodsReportDataSet.CreatedUtcField, schema.DataSet.DefaultDateField);
        Assert.DoesNotContain(schema.Fields, field => field.Name is "Last4" or "CustomerReference" or "PaymentMethodReference" or "SourceSessionId");

        Assert.Equal(["method-2", "method-1"], rows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        var kept = rows[1];
        Assert.Equal("Stripe", kept["ProviderKey"]);
        Assert.Equal("visa", kept["Brand"]);
        Assert.Equal(12L, kept["ExpirationMonth"]);
        Assert.Equal(2030L, kept["ExpirationYear"]);
        Assert.Equal(true, kept["IsDefault"]);
        Assert.Equal("user-1", kept["UserId"]);
        Assert.All(rows, row => Assert.DoesNotContain(row.Values, value => value is string text && (text.Contains("secret", StringComparison.Ordinal) || text.Contains("4242", StringComparison.Ordinal) || text.Contains("5454", StringComparison.Ordinal))));

        Assert.Equal(["method-2"], userRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
    }

    [Fact]
    public async Task ReportIndexMigrations_CanRunAgain()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(_store.Configuration, transaction);

        // Act
        var checkout = await new CheckoutMigrations { SchemaBuilder = schemaBuilder }.UpdateFrom1Async();
        var checkoutAgain = await new CheckoutMigrations { SchemaBuilder = schemaBuilder }.UpdateFrom1Async();
        var coupons = await new CouponMigrations { SchemaBuilder = schemaBuilder }.UpdateFrom1Async();
        var couponsAgain = await new CouponMigrations { SchemaBuilder = schemaBuilder }.UpdateFrom1Async();

        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, checkout);
        Assert.Equal(2, checkoutAgain);
        Assert.Equal(2, coupons);
        Assert.Equal(2, couponsAgain);
    }

    [Fact]
    public async Task DataSourcesStartup_RegistersTheDataSourceAndItsDataSets()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var services = new ServiceCollection();

        services.AddSingleton(session);
        services.AddSingleton(Authorization(_allGrants));
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>));

        // Act
        new DataSourcesStartup().ConfigureServices(services);

        await using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredService<CheckoutReportDataSource>();
        var dataSets = await source.GetDataSetsAsync(new DataSourceContext { User = _user }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(CommerceReportDataSets.CheckoutSource.Name, CheckoutReportDataSource.Name);
        Assert.Equal(4, dataSets.Count);
    }

    private static CheckoutSession CheckoutSession(int day)
    {
        var started = _start.AddDays(day);

        var session = new CheckoutSession
        {
            SessionId = $"session-{day}",
            ReferenceType = day == 2 ? "Transaction" : CheckoutReferenceTypes.Order,
            ReferenceId = day == 2 ? "tx-1" : $"order-{day}",
            Currency = "USD",
            Status = day switch
            {
                1 => CheckoutSessionStatus.Expired,
                2 => CheckoutSessionStatus.Pending,
                _ => CheckoutSessionStatus.Completed,
            },
            SettlesExistingBalance = day == 2,
            OwnerId = day switch
            {
                1 => null,
                2 => "user-2",
                _ => "user-1",
            },
            CurrentStep = day == 1 ? "Payment" : null,
            IPAddress = "203.0.113.5",
            AgentInfo = "Mozilla/5.0",
            GuestTokenHash = "guest-token-hash",
            CreatedUtc = started,
            ModifiedUtc = day == 1 ? started.AddHours(24) : started.AddMinutes(10),
            CompletedUtc = day is 0 or 3 ? started.AddMinutes(10) : null,
        };

        session.Put(new CheckoutContactInfo
        {
            DisplayName = "Ada",
            Email = "ada@example.com",
            Phone = "+15550000000",
            BillingAddress = new Address { Country = "DE", Region = "BE", City = "Berlin", AddressLine1 = "1 Private Street" },
        });

        if (day != 2)
        {
            session.Put(new CheckoutInvoice
            {
                Currency = "USD",
                DueNow = 110m,
                TaxAmount = 10m,
                GrandTotal = 110m,
                LineItems =
                [
                    new CheckoutLineItem { ItemId = "line-a", Description = "Mug", Quantity = 2, UnitPrice = 20m },
                    new CheckoutLineItem { ItemId = "line-b", Description = "Cap", Quantity = 1, UnitPrice = 60m },
                ],
                Discounts = [new DiscountLine { ProviderKey = "Coupons", Code = "SAVE5", Amount = 5m }],
                Tenders = [new TenderLine { ProviderKey = "GiftCards", Amount = 20m }],
            });
        }

        if (day == 3)
        {
            session.Anonymize(started.AddDays(30));
        }

        return session;
    }

    private static CheckoutReportDataSource Source(ISession session, string[] grants = null)
    {
        var authorizationService = Authorization(grants ?? _allGrants);

        return new CheckoutReportDataSource(
        [
            new CheckoutSessionsReportDataSet(session, authorizationService, new PassThroughStringLocalizer<CheckoutSessionsReportDataSet>()),
            new CouponsReportDataSet(session, authorizationService, new PassThroughStringLocalizer<CouponsReportDataSet>()),
            new CouponRedemptionsReportDataSet(session, authorizationService, new PassThroughStringLocalizer<CouponRedemptionsReportDataSet>()),
            new SavedPaymentMethodsReportDataSet(session, authorizationService, new PassThroughStringLocalizer<SavedPaymentMethodsReportDataSet>()),
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
