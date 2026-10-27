using System.Security.Claims;
using CrestApps.OrchardCore.Checkout.Migrations;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module;
using CrestApps.OrchardCore.Tests.Utilities;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Core;
using CrestApps.OrchardCore.Transactions.Core.Indexes;
using CrestApps.OrchardCore.Transactions.Migrations;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Reports.DataSources;
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

namespace CrestApps.OrchardCore.Tests.Transactions;

/// <summary>
/// The transactions data source reads real transactions, payments and refunds from a SQLite store, so the index queries,
/// the fields they expose, the push-down of dates, join keys and statuses, the grouping in the database, and the
/// permissions they require are all exercised end to end.
/// </summary>
public sealed class TransactionReportDataSourceTests : IAsyncLifetime
{
    private static readonly ClaimsPrincipal _user = ReportDesignerPrincipals.User("u1", "Manager");
    private static readonly DateTime _start = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"transaction-reports-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_databasePath};Pooling=False"));
        _store.RegisterIndexes([new TransactionIndexProvider()], TransactionsConstants.CollectionName);
        _store.RegisterIndexes([new PaymentAttemptIndexProvider(), new PaymentRefundIndexProvider()]);

        await _store.InitializeAsync(cancellationToken);
        await _store.InitializeCollectionAsync(TransactionsConstants.CollectionName, cancellationToken);

        await using (var session = _store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(cancellationToken);
            var schemaBuilder = new SchemaBuilder(_store.Configuration, transaction);

            await new TransactionMigrations { SchemaBuilder = schemaBuilder }.CreateAsync();
            await new CheckoutMigrations { SchemaBuilder = schemaBuilder }.CreateAsync();

            await transaction.CommitAsync(cancellationToken);
        }

        await using (var session = _store.CreateSession())
        {
            // An outstanding order balance, a guest's partly paid charge, and a settled one, a day apart.
            await session.SaveAsync(new Transaction
            {
                ItemId = "tx-0",
                Title = "Order N-1",
                Source = "PayLater",
                OwnerId = "user-1",
                OwnerKind = CustomerOwnerKind.Authenticated,
                ReferenceType = "Order",
                ReferenceId = "order-1",
                CheckoutSessionId = "session-1",
                ObligationId = "obligation-1",
                PaymentAttemptId = "attempt-1",
                Currency = "USD",
                Amount = 100m,
                TaxAmount = 10m,
                TotalAmount = 110m,
                Status = TransactionStatus.Outstanding,
                DueUtc = _start.AddDays(30),
                SettlementReference = "Bank slip 42",
                Events = [new TransactionEvent { Id = "event-1", Type = TransactionEventType.Created, Message = "Private message", CreatedUtc = _start }],
                CreatedUtc = _start,
                UpdatedUtc = _start,
            }, false, TransactionsConstants.CollectionName, cancellationToken);

            await session.SaveAsync(new Transaction
            {
                ItemId = "tx-1",
                Title = "Membership",
                Source = "PayLater",
                OwnerId = "guest-1",
                OwnerKind = CustomerOwnerKind.Guest,
                GuestContactName = "Grace",
                GuestContactEmail = "grace@example.com",
                ReferenceType = "Subscription",
                ReferenceId = "subscription-1",
                Currency = "EUR",
                Amount = 50m,
                TotalAmount = 50m,
                AmountPaid = 20m,
                Status = TransactionStatus.PartiallyPaid,
                CreatedUtc = _start.AddDays(1),
                UpdatedUtc = _start.AddDays(1),
            }, false, TransactionsConstants.CollectionName, cancellationToken);

            await session.SaveAsync(new Transaction
            {
                ItemId = "tx-2",
                Title = "Order N-2",
                Source = "Stripe",
                OwnerId = "user-1",
                OwnerKind = CustomerOwnerKind.Authenticated,
                Currency = "USD",
                Amount = 30m,
                TotalAmount = 30m,
                AmountPaid = 30m,
                Status = TransactionStatus.Paid,
                SettledUtc = _start.AddDays(2).AddHours(1),
                SettlementMethod = TransactionsConstants.SettlementMethods.Online,
                CreatedUtc = _start.AddDays(2),
                UpdatedUtc = _start.AddDays(2),
            }, false, TransactionsConstants.CollectionName, cancellationToken);

            await session.SaveAsync(new PaymentAttempt
            {
                ItemId = "attempt-1",
                SessionId = "session-1",
                ReferenceType = TransactionsConstants.ReferenceTypes.Transaction,
                ReferenceId = "tx-0",
                ProviderKey = "Stripe",
                ObligationId = "obligation-1",
                IdempotencyKey = "secret-idempotency-key",
                ProviderReference = "pi_123",
                TransactionId = "gateway-transaction-1",
                Currency = "USD",
                ExpectedAmount = 100m,
                ExpectedTaxAmount = 10m,
                ConfirmedAmount = 100m,
                ConfirmedTaxAmount = 10m,
                State = PaymentAttemptState.Succeeded,
                GatewayMode = GatewayMode.Testing,
                CreatedUtc = _start,
                UpdatedUtc = _start.AddMinutes(1),
            }, false, null, cancellationToken);

            await session.SaveAsync(new PaymentAttempt
            {
                ItemId = "attempt-2",
                SessionId = "session-2",
                ReferenceType = "Order",
                ReferenceId = "order-2",
                ProviderKey = "Stripe",
                IdempotencyKey = "another-secret-key",
                Currency = "USD",
                ExpectedAmount = 40m,
                State = PaymentAttemptState.Failed,
                FailureReason = "Card declined",
                CreatedUtc = _start.AddDays(1),
                UpdatedUtc = _start.AddDays(1),
            }, false, null, cancellationToken);

            await session.SaveAsync(new PaymentRefund
            {
                ItemId = "refund-1",
                SessionId = "session-1",
                ProviderKey = "Stripe",
                OriginalAttemptId = "attempt-1",
                OriginalTransactionId = "pi_123",
                ReferenceType = TransactionsConstants.ReferenceTypes.Transaction,
                ReferenceId = "tx-0",
                ProviderRefundReference = "re_1",
                IdempotencyKey = "refund-secret-key",
                Currency = "USD",
                RefundGrossAmount = 22m,
                RefundTaxAmount = 2m,
                RefundTaxableAmount = 20m,
                Status = RefundStatus.Succeeded,
                Reason = "Damaged",
                CreatedUtc = _start.AddDays(2),
                UpdatedUtc = _start.AddDays(2),
                CompletedUtc = _start.AddDays(2),
            }, false, null, cancellationToken);

            await session.SaveAsync(new PaymentRefund
            {
                ItemId = "refund-2",
                SessionId = "session-2",
                ProviderKey = "Cash",
                Currency = "USD",
                RefundGrossAmount = 5m,
                Status = RefundStatus.PendingManualReview,
                CreatedUtc = _start.AddDays(3),
                UpdatedUtc = _start.AddDays(3),
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
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.TransactionSource.Transactions, context, TestContext.Current.CancellationToken);
        var transactions = await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.TransactionSource.Transactions), TestContext.Current.CancellationToken);
        var payments = await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.TransactionSource.PaymentAttempts), TestContext.Current.CancellationToken);
        var refunds = await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.TransactionSource.PaymentRefunds), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(dataSets);
        Assert.Null(schema);
        Assert.Empty(transactions.Rows);
        Assert.Empty(payments.Rows);
        Assert.Empty(refunds.Rows);
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
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.TransactionSource.Transactions, context, TestContext.Current.CancellationToken);
        var rows = await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.TransactionSource.Transactions, context: context), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(dataSets);
        Assert.Null(schema);
        Assert.Empty(rows.Rows);
    }

    [Fact]
    public async Task EachLedger_NeedsItsOwnPermission()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var context = new DataSourceContext { User = _user };
        var transactionsOnly = Source(session, grants: [TransactionsPermissions.ManageTransactions.Name]);
        var refundsOnly = Source(session, grants: [TransactionsPermissions.ManageRefunds.Name]);

        // Act
        var transactionDataSets = await transactionsOnly.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var paymentSchema = await transactionsOnly.GetSchemaAsync(CommerceReportDataSets.TransactionSource.PaymentAttempts, context, TestContext.Current.CancellationToken);
        var refundDataSets = await refundsOnly.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var transactionSchema = await refundsOnly.GetSchemaAsync(CommerceReportDataSets.TransactionSource.Transactions, context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([CommerceReportDataSets.TransactionSource.Transactions], transactionDataSets.Select(dataSet => dataSet.Name));
        Assert.Null(paymentSchema);
        Assert.Equal([CommerceReportDataSets.TransactionSource.PaymentAttempts, CommerceReportDataSets.TransactionSource.PaymentRefunds], refundDataSets.Select(dataSet => dataSet.Name));
        Assert.Null(transactionSchema);
    }

    [Fact]
    public async Task Schema_Transactions_DeclaresKeysAndReferences_AndNeverExposesPrivateDetails()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var context = new DataSourceContext { User = _user };

        // Act
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.TransactionSource.Transactions, context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TransactionsReportDataSet.CreatedUtcField, schema.DataSet.DefaultDateField);
        Assert.True(schema.FindField(CommerceReportDataSets.ItemIdField).IsIdentifier);
        Assert.True(schema.FindField(CommerceReportDataSets.ItemIdField).IsKeyFilterable);
        Assert.True(schema.FindField("OwnerId").IsKeyFilterable);
        Assert.True(schema.FindField("CheckoutSessionId").IsKeyFilterable);
        Assert.False(schema.FindField("UserId").IsKeyFilterable);

        var user = Assert.Single(schema.FindField("UserId").References);
        Assert.Equal(ReportsConstants.UsersDataSource, user.Source);
        Assert.Equal(ReportsConstants.UserIdField, user.Field);

        var checkout = Assert.Single(schema.FindField("CheckoutSessionId").References);
        Assert.Equal(CommerceReportDataSets.CheckoutSource.Name, checkout.Source);
        Assert.Equal(CommerceReportDataSets.CheckoutSource.CheckoutSessions, checkout.DataSet);
        Assert.Equal(CommerceReportDataSets.ItemIdField, checkout.Field);

        var order = Assert.Single(schema.FindField("OrderId").References);
        Assert.Equal(CommerceReportDataSets.OrderSource.Name, order.Source);
        Assert.Equal(CommerceReportDataSets.OrderSource.Orders, order.DataSet);

        Assert.Equal(DataFieldType.Decimal, schema.FindField("OutstandingAmount").Type);
        Assert.False(string.IsNullOrEmpty(schema.FindField("OutstandingAmount").Description));
        Assert.Equal(DataFieldType.Text, schema.FindField("Currency").Type);
        Assert.DoesNotContain(schema.Fields, field => field.Name is "Events" or "SettlementReference" or "ObligationId" or "PaymentAttemptId" or "AutoCollection" or "Recurrence");
    }

    [Fact]
    public async Task Schema_PaymentsAndRefunds_NeverExposeTheirKeys()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var context = new DataSourceContext { User = _user };

        // Act
        var payments = await source.GetSchemaAsync(CommerceReportDataSets.TransactionSource.PaymentAttempts, context, TestContext.Current.CancellationToken);
        var refunds = await source.GetSchemaAsync(CommerceReportDataSets.TransactionSource.PaymentRefunds, context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(PaymentAttemptsReportDataSet.CreatedUtcField, payments.DataSet.DefaultDateField);
        Assert.Null(refunds.DataSet.DefaultDateField);
        Assert.DoesNotContain(payments.Fields, field => field.Name is "IdempotencyKey" or "ObligationId" or "TaxSnapshot");
        Assert.DoesNotContain(refunds.Fields, field => field.Name is "IdempotencyKey" or "ObligationId" or "TaxLines" or "OriginalTransactionId" or "ProviderRefundReference");

        var transaction = Assert.Single(payments.FindField("TransactionId").References);
        Assert.Equal(CommerceReportDataSets.TransactionSource.Name, transaction.Source);
        Assert.Equal(CommerceReportDataSets.TransactionSource.Transactions, transaction.DataSet);

        var payment = Assert.Single(refunds.FindField("OriginalAttemptId").References);
        Assert.Equal(CommerceReportDataSets.TransactionSource.PaymentAttempts, payment.DataSet);
        Assert.True(refunds.FindField("SessionId").IsKeyFilterable);
        Assert.False(refunds.FindField("OriginalAttemptId").IsKeyFilterable);
    }

    [Fact]
    public async Task Query_Transactions_ReturnsTypedValues_NewestFirst()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);

        // Act
        var rows = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.TransactionSource.Transactions), TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["tx-2", "tx-1", "tx-0"], rows.Select(row => row[CommerceReportDataSets.ItemIdField]));

        var outstanding = rows[2];
        Assert.Equal("Outstanding", outstanding["Status"]);
        Assert.Equal("USD", outstanding["Currency"]);
        Assert.Equal(110m, outstanding["TotalAmount"]);
        Assert.Equal(110m, outstanding["OutstandingAmount"]);
        Assert.Equal(0m, outstanding["AmountPaid"]);
        Assert.Equal("order-1", outstanding["OrderId"]);
        Assert.Equal("session-1", outstanding["CheckoutSessionId"]);
        Assert.Equal("user-1", outstanding["UserId"]);
        Assert.Equal(false, outstanding["IsGuest"]);
        Assert.Equal("Authenticated", outstanding["OwnerKind"]);
        Assert.Equal(DateTimeKind.Utc, Assert.IsType<DateTime>(outstanding["DueUtc"]).Kind);
        Assert.DoesNotContain(outstanding.Values, value => value is string text && (text.Contains("Bank slip", StringComparison.Ordinal) || text.Contains("Private", StringComparison.Ordinal)));

        var guest = rows[1];
        Assert.Equal("PartiallyPaid", guest["Status"]);
        Assert.Equal("EUR", guest["Currency"]);
        Assert.Equal(30m, guest["OutstandingAmount"]);
        Assert.Equal(true, guest["IsGuest"]);
        Assert.Null(guest["UserId"]);
        Assert.Equal("guest-1", guest["OwnerId"]);
        Assert.Equal("Grace", guest["GuestContactName"]);
        Assert.Null(guest["OrderId"]);

        var paid = rows[0];
        Assert.Equal(0m, paid["OutstandingAmount"]);
        Assert.Equal("online", paid["SettlementMethod"]);
        Assert.NotNull(paid["SettledUtc"]);
    }

    [Fact]
    public async Task Query_Transactions_PushDownNeverDropsAMatchingRow()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var dates = Query(CommerceReportDataSets.TransactionSource.Transactions, CommerceReportDataSets.ItemIdField);
        dates.Conditions.Add(Condition(TransactionsReportDataSet.CreatedUtcField, DataFilterOperator.Between, _start.AddDays(1), _start.AddDays(2)));
        var owner = Query(CommerceReportDataSets.TransactionSource.Transactions, CommerceReportDataSets.ItemIdField);
        owner.Conditions.Add(new DataCondition { Field = "OwnerId", Operator = DataFilterOperator.In, Values = ["user-1"], IsJoinKey = true });
        var outstanding = Query(CommerceReportDataSets.TransactionSource.Transactions, CommerceReportDataSets.ItemIdField);
        outstanding.Conditions.Add(Condition("Status", DataFilterOperator.In, "outstanding", "PartiallyPaid"));
        var currency = Query(CommerceReportDataSets.TransactionSource.Transactions, CommerceReportDataSets.ItemIdField);
        currency.Conditions.Add(Condition("Currency", DataFilterOperator.Equals, "eur"));
        var none = Query(CommerceReportDataSets.TransactionSource.Transactions, CommerceReportDataSets.ItemIdField);
        none.Conditions.Add(new DataCondition { Field = CommerceReportDataSets.ItemIdField, Operator = DataFilterOperator.In, Values = [], IsJoinKey = true });

        // Act
        var dateRows = Rows(await ReportDesignerTestServices.ReadAsync(source, dates, TestContext.Current.CancellationToken));
        var ownerRows = Rows(await ReportDesignerTestServices.ReadAsync(source, owner, TestContext.Current.CancellationToken));
        var outstandingRows = Rows(await ReportDesignerTestServices.ReadAsync(source, outstanding, TestContext.Current.CancellationToken));
        var currencyRows = Rows(await ReportDesignerTestServices.ReadAsync(source, currency, TestContext.Current.CancellationToken));
        var noRows = Rows(await ReportDesignerTestServices.ReadAsync(source, none, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["tx-2", "tx-1"], dateRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal(["tx-2", "tx-0"], ownerRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal(["tx-1", "tx-0"], outstandingRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal(["tx-1"], currencyRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Empty(noRows);
    }

    [Fact]
    public async Task Aggregate_Transactions_SumsWhatIsOutstandingInTheDatabase()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var query = new DataAggregateQuery
        {
            DataSet = CommerceReportDataSets.TransactionSource.Transactions,
            Groups = [new DataAggregateGroup { Field = "Currency" }],
            Measures =
            [
                new DataAggregateMeasure { Function = DataAggregate.Count },
                new DataAggregateMeasure { Field = "OutstandingAmount", Function = DataAggregate.Sum },
            ],
            Context = new DataSourceContext { User = _user },
        };

        var parent = new DataAggregateQuery
        {
            DataSet = CommerceReportDataSets.TransactionSource.Transactions,
            Groups = [new DataAggregateGroup { Field = "ParentReferenceId" }],
            Measures = [new DataAggregateMeasure { Function = DataAggregate.Count }],
            Context = new DataSourceContext { User = _user },
        };

        var unindexed = new DataAggregateQuery
        {
            DataSet = CommerceReportDataSets.TransactionSource.Transactions,
            Measures = [new DataAggregateMeasure { Function = DataAggregate.Count }],
            Context = new DataSourceContext { User = _user },
        };

        unindexed.Conditions.Add(Condition("Amount", DataFilterOperator.GreaterThan, 40m));

        // Act
        var groups = await ((IDataAggregateSource)source).AggregateAsync(query, TestContext.Current.CancellationToken);
        var declined = await ((IDataAggregateSource)source).AggregateAsync(parent, TestContext.Current.CancellationToken);
        var filtered = await ((IDataAggregateSource)source).AggregateAsync(unindexed, TestContext.Current.CancellationToken);

        // Assert
        // The database groups text by its own collation, so a group by the currency or the parent is left to the report;
        // a filter on the currency is still applied in the database.
        Assert.Null(groups);
        Assert.Null(declined);

        foreach (var (currency, count, outstanding) in new[] { ("USD", 2L, 110m), ("eur", 1L, 30m) })
        {
            var totals = await ((IDataAggregateSource)source).AggregateAsync(new DataAggregateQuery
            {
                DataSet = CommerceReportDataSets.TransactionSource.Transactions,
                Measures = query.Measures,
                Conditions = [Condition("Currency", DataFilterOperator.Equals, currency)],
                Context = new DataSourceContext { User = _user },
            }, TestContext.Current.CancellationToken);

            Assert.NotNull(totals);
            var row = Assert.Single(totals);
            Assert.Equal(count, (long)row[0]);
            Assert.Equal(outstanding, Convert.ToDecimal(row[1], System.Globalization.CultureInfo.InvariantCulture));
        }

        // A condition on a field the index doesn't hold can't be answered in the database.
        Assert.Null(filtered);
    }

    [Fact]
    public async Task Query_PaymentAttempts_ShowWhatEachPaymentWasFor()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var bySession = Query(CommerceReportDataSets.TransactionSource.PaymentAttempts, CommerceReportDataSets.ItemIdField);
        bySession.Conditions.Add(new DataCondition { Field = "SessionId", Operator = DataFilterOperator.In, Values = ["session-1"], IsJoinKey = true });
        var failed = Query(CommerceReportDataSets.TransactionSource.PaymentAttempts, CommerceReportDataSets.ItemIdField);
        failed.Conditions.Add(Condition("State", DataFilterOperator.In, "failed"));

        // Act
        var rows = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.TransactionSource.PaymentAttempts), TestContext.Current.CancellationToken));
        var sessionRows = Rows(await ReportDesignerTestServices.ReadAsync(source, bySession, TestContext.Current.CancellationToken));
        var failedRows = Rows(await ReportDesignerTestServices.ReadAsync(source, failed, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["attempt-2", "attempt-1"], rows.Select(row => row[CommerceReportDataSets.ItemIdField]));

        var succeeded = rows[1];
        Assert.Equal("Succeeded", succeeded["State"]);
        Assert.Equal("tx-0", succeeded["TransactionId"]);
        Assert.Null(succeeded["OrderId"]);
        Assert.Equal(110m, succeeded["ConfirmedTotal"]);
        Assert.Equal("Testing", succeeded["GatewayMode"]);
        Assert.Equal("pi_123", succeeded["ProviderReference"]);
        Assert.DoesNotContain(succeeded.Values, value => value is string text && (text.Contains("secret", StringComparison.Ordinal) || text.Contains("gateway-transaction", StringComparison.Ordinal)));

        var declined = rows[0];
        Assert.Equal("order-2", declined["OrderId"]);
        Assert.Null(declined["TransactionId"]);
        Assert.Equal("Card declined", declined["FailureReason"]);
        Assert.Equal(0m, declined["ConfirmedTotal"]);

        Assert.Equal(["attempt-1"], sessionRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal(["attempt-2"], failedRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
    }

    [Fact]
    public async Task Query_PaymentRefunds_AreReadNewestFirst_AndADateRangeKeepsTheMatchingRefund()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var dates = Query(CommerceReportDataSets.TransactionSource.PaymentRefunds, CommerceReportDataSets.ItemIdField);
        dates.Conditions.Add(Condition(PaymentRefundsReportDataSet.CreatedUtcField, DataFilterOperator.Between, _start.AddDays(2), _start.AddDays(2).AddHours(1)));
        var status = Query(CommerceReportDataSets.TransactionSource.PaymentRefunds, CommerceReportDataSets.ItemIdField);
        status.Conditions.Add(Condition("Status", DataFilterOperator.In, "PendingManualReview"));

        // Act
        var rows = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.TransactionSource.PaymentRefunds), TestContext.Current.CancellationToken));
        var dateRows = Rows(await ReportDesignerTestServices.ReadAsync(source, dates, TestContext.Current.CancellationToken));
        var statusRows = Rows(await ReportDesignerTestServices.ReadAsync(source, status, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["refund-2", "refund-1"], rows.Select(row => row[CommerceReportDataSets.ItemIdField]));

        var refund = rows[1];
        Assert.Equal("Succeeded", refund["Status"]);
        Assert.Equal(22m, refund["RefundGrossAmount"]);
        Assert.Equal(2m, refund["RefundTaxAmount"]);
        Assert.Equal("attempt-1", refund["OriginalAttemptId"]);
        Assert.Equal("tx-0", refund["TransactionId"]);
        Assert.Equal("Damaged", refund["Reason"]);
        Assert.DoesNotContain(refund.Values, value => value is string text && (text.Contains("secret", StringComparison.Ordinal) || text == "re_1"));

        Assert.Contains(dateRows, row => (string)row[CommerceReportDataSets.ItemIdField] == "refund-1");
        Assert.Equal(["refund-2"], statusRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
    }

    [Fact]
    public async Task ReportIndexMigration_CanRunAgain()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(_store.Configuration, transaction);

        // Act
        var version = await new TransactionMigrations { SchemaBuilder = schemaBuilder }.UpdateFrom1Async();
        var again = await new TransactionMigrations { SchemaBuilder = schemaBuilder }.UpdateFrom1Async();

        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, version);
        Assert.Equal(2, again);
    }

    [Fact]
    public async Task DataSourcesStartups_RegisterTheLedgersOnlyWithCheckout()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var withoutCheckout = Services(session);
        var withCheckout = Services(session);

        // Act
        new DataSourcesStartup().ConfigureServices(withoutCheckout);
        new DataSourcesStartup().ConfigureServices(withCheckout);
        new CheckoutDataSourcesStartup().ConfigureServices(withCheckout);

        await using var withoutProvider = withoutCheckout.BuildServiceProvider();
        await using var withProvider = withCheckout.BuildServiceProvider();
        var alone = await withoutProvider.GetRequiredService<TransactionReportDataSource>().GetDataSetsAsync(new DataSourceContext { User = _user }, TestContext.Current.CancellationToken);
        var all = await withProvider.GetRequiredService<TransactionReportDataSource>().GetDataSetsAsync(new DataSourceContext { User = _user }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(CommerceReportDataSets.TransactionSource.Name, TransactionReportDataSource.Name);
        Assert.Equal([CommerceReportDataSets.TransactionSource.Transactions], alone.Select(dataSet => dataSet.Name));
        Assert.Equal(3, all.Count);
    }

    private static ServiceCollection Services(ISession session)
    {
        var services = new ServiceCollection();

        services.AddSingleton(session);
        services.AddSingleton(Authorization([TransactionsPermissions.ManageTransactions.Name, TransactionsPermissions.ManageRefunds.Name]));
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>));

        return services;
    }

    private static TransactionReportDataSource Source(ISession session, string[] grants = null)
    {
        var authorizationService = Authorization(grants ?? [TransactionsPermissions.ManageTransactions.Name, TransactionsPermissions.ManageRefunds.Name]);

        return new TransactionReportDataSource(
        [
            new TransactionsReportDataSet(session, authorizationService, new PassThroughStringLocalizer<TransactionsReportDataSet>()),
            new PaymentAttemptsReportDataSet(session, authorizationService, new PassThroughStringLocalizer<PaymentAttemptsReportDataSet>()),
            new PaymentRefundsReportDataSet(session, authorizationService, new PassThroughStringLocalizer<PaymentRefundsReportDataSet>()),
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
        return Query(dataSet, null, fields);
    }

    private static DataSourceQuery Query(string dataSet, DataSourceContext context, params string[] fields)
    {
        return new DataSourceQuery
        {
            DataSet = dataSet,
            Fields = new HashSet<string>(fields, StringComparer.Ordinal),
            MaxRows = 100,
            Context = context ?? new DataSourceContext { User = _user },
        };
    }

    private static List<Dictionary<string, object>> Rows(DataRowSet table)
    {
        return table.Rows
            .Select(row => table.Fields.Select((field, index) => (field.Name, Value: row[index])).ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal))
            .ToList();
    }
}
