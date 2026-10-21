using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Receipts;
using CrestApps.OrchardCore.Receipts.Models;
using CrestApps.OrchardCore.Tests.Checkout;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Core;
using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Transactions.FinancialDocuments;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using CrestApps.OrchardCore.Transactions.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Locking;
using OrchardCore.Notifications;
using OrchardCore.Users;
using OrchardCore.Users.Services;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Transactions;

/// <summary>
/// The actions staff take on a transaction live in one service the console and other modules share, so a module that
/// offers them on its own screens gets the same timeline, receipts and handler calls as the Commerce console.
/// </summary>
public sealed class TransactionCommandServiceTests
{
    private static readonly DateTime _now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task RecordOfflinePaymentAsync_ForPartOfTheBalance_RecordsItAndTellsThePaymentHandlers()
    {
        // Arrange
        var context = new TestContextBuilder(CreateOutstanding());

        // Act
        var result = await context.Service.RecordOfflinePaymentAsync("transaction-1", 40m, "Check 1042", "Paid at the front desk.", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(TransactionStatus.PartiallyPaid, result.Transaction.Status);
        Assert.Equal(40m, result.Transaction.AmountPaid);
        Assert.Equal(60m, result.Transaction.OutstandingAmount);
        Assert.Equal(TransactionEventType.PaymentRecorded, result.Event.Type);
        Assert.Equal("Check 1042", result.Event.Reference);
        Assert.Equal(TransactionsConstants.SettlementMethods.Offline, result.Event.Method);
        Assert.Contains("Check 1042", result.Event.Message, StringComparison.Ordinal);
        Assert.Equal([result.Event], context.Payments);
        Assert.Equal([("transaction-1", TransactionStatus.Outstanding, TransactionStatus.PartiallyPaid)], context.Events.StatusChanges);
    }

    [Fact]
    public async Task RecordOfflinePaymentAsync_ForMoreThanIsOwed_RecordsOnlyWhatIsOwedAndSettles()
    {
        // Arrange
        var context = new TestContextBuilder(CreateOutstanding());

        // Act
        var result = await context.Service.RecordOfflinePaymentAsync("transaction-1", 250m, null, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(TransactionStatus.Paid, result.Transaction.Status);
        Assert.Equal(100m, result.Event.Amount);
        Assert.Equal(_now, result.Transaction.SettledUtc);
    }

    [Fact]
    public async Task RecordOfflinePaymentAsync_OnACanceledTransaction_IsRefused()
    {
        // Arrange
        var canceled = CreateOutstanding();
        canceled.Status = TransactionStatus.Canceled;

        var context = new TestContextBuilder(canceled);

        // Act
        var result = await context.Service.RecordOfflinePaymentAsync("transaction-1", 40m, null, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(0m, canceled.AmountPaid);
        Assert.Empty(context.Payments);
        Assert.Equal(0, context.Events.Count);
    }

    [Fact]
    public async Task Command_ForAnUnknownTransaction_ReturnsNoTransaction()
    {
        // Arrange
        var context = new TestContextBuilder(CreateOutstanding());

        // Act
        var result = await context.Service.CancelAsync("missing", null, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Null(result.Transaction);
    }

    [Fact]
    public async Task MarkPaidAsync_AfterAnAdjustment_RecordsOnlyWhatWasStillOwedAsPaid()
    {
        // Arrange
        var context = new TestContextBuilder(CreateOutstanding());

        await context.Service.AdjustAsync("transaction-1", 25m, TransactionAdjustmentKind.Discount, "Sibling discount", TestContext.Current.CancellationToken);

        // Act
        var result = await context.Service.MarkPaidAsync("transaction-1", "Paid in cash.", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(TransactionStatus.Paid, result.Transaction.Status);
        Assert.Equal(75m, result.Transaction.AmountPaid);
        Assert.Equal(75m, result.Event.Amount);
        Assert.Equal([result.Event], context.Payments);
    }

    [Fact]
    public async Task CancelAsync_RecordsTheReasonAndRaisesCanceledOnce()
    {
        // Arrange
        var context = new TestContextBuilder(CreateOutstanding());

        // Act
        var result = await context.Service.CancelAsync("transaction-1", "  Registration withdrawn.  ", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(TransactionStatus.Canceled, result.Transaction.Status);
        Assert.Equal("Registration withdrawn.", result.Event.Reason);
        Assert.Equal([("transaction-1", TransactionStatus.Outstanding, TransactionStatus.Canceled)], context.Events.StatusChanges);
        Assert.Equal([("transaction-1", "Registration withdrawn.")], context.Events.Canceled);
    }

    [Fact]
    public async Task CancelAsync_OnAPaidTransaction_IsRefused()
    {
        // Arrange
        var paid = CreateOutstanding();
        paid.AmountPaid = paid.TotalAmount;
        paid.Status = TransactionStatus.Paid;

        var context = new TestContextBuilder(paid);

        // Act
        var result = await context.Service.CancelAsync("transaction-1", null, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(TransactionStatus.Paid, paid.Status);
    }

    [Fact]
    public async Task AdjustAsync_ForPartOfTheBalance_LowersWhatIsOwedWithoutCountingItAsPaid()
    {
        // Arrange
        var context = new TestContextBuilder(CreateOutstanding());

        // Act
        var result = await context.Service.AdjustAsync("transaction-1", 30m, TransactionAdjustmentKind.Waiver, "Financial aid", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(30m, result.Transaction.AmountAdjusted);
        Assert.Equal(0m, result.Transaction.AmountPaid);
        Assert.Equal(70m, result.Transaction.OutstandingAmount);
        Assert.Equal(TransactionStatus.Outstanding, result.Transaction.Status);
        Assert.Equal(TransactionEventType.Adjusted, result.Event.Type);
        Assert.Equal(TransactionAdjustmentKind.Waiver, result.Event.AdjustmentKind);
        Assert.Equal(30m, result.Event.Amount);
        Assert.Equal("Financial aid", result.Event.Reason);
        Assert.Empty(context.Payments);
        Assert.Equal(0, context.Events.Count);
    }

    [Theory]
    [InlineData(100.01)]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task AdjustAsync_ForMoreThanIsOwedOrNothing_IsRefused(double amount)
    {
        // Arrange
        var context = new TestContextBuilder(CreateOutstanding());

        // Act
        var result = await context.Service.AdjustAsync("transaction-1", (decimal)amount, TransactionAdjustmentKind.Discount, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(0m, result.Transaction.AmountAdjusted);
        Assert.DoesNotContain(result.Transaction.Events, evt => evt.Type == TransactionEventType.Adjusted);
    }

    [Fact]
    public async Task AdjustAsync_ForEverythingOwed_SettlesTheTransactionAndItIsNeverReminded()
    {
        // Arrange
        var outstanding = CreateOutstanding();
        outstanding.DueUtc = _now.AddDays(-10);

        var context = new TestContextBuilder(outstanding);

        // Act
        var result = await context.Service.AdjustAsync("transaction-1", 100m, TransactionAdjustmentKind.Waiver, "Scholarship", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(TransactionStatus.Paid, result.Transaction.Status);
        Assert.Equal(0m, result.Transaction.OutstandingAmount);
        Assert.Equal(TransactionsConstants.SettlementMethods.Adjusted, result.Transaction.SettlementMethod);
        Assert.Equal([("transaction-1", TransactionStatus.Outstanding, TransactionStatus.Paid)], context.Events.StatusChanges);
        Assert.Empty(context.Payments);
        Assert.False(TransactionStateMachine.CanSendReminder(result.Transaction));
        Assert.False(TransactionReminderBackgroundTask.IsReminderDue(result.Transaction, new TransactionReminderSettings(), _now));
        Assert.Empty(await context.Manager.GetOutstandingDueAsync(_now, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AdjustAsync_ThenAnOnlinePaymentOfTheRest_SettlesInFull()
    {
        // Arrange
        var context = new TestContextBuilder(CreateOutstanding());
        var settlement = new TransactionSettlementService(
            context.Manager,
            new ServiceCollection().BuildServiceProvider(),
            context.Clock,
            NullLogger<TransactionSettlementService>.Instance,
            new PassThroughStringLocalizer<TransactionSettlementService>());

        await context.Service.AdjustAsync("transaction-1", 20m, TransactionAdjustmentKind.Discount, null, TestContext.Current.CancellationToken);

        var transaction = await context.Manager.FindByIdAsync("transaction-1", TestContext.Current.CancellationToken);

        // Act
        await settlement.ApplyAsync(transaction, [CreateAttempt("attempt-1", 80m, _now)], "session-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TransactionStatus.Paid, transaction.Status);
        Assert.Equal(0m, transaction.OutstandingAmount);
    }

    [Fact]
    public async Task SendInvoiceAsync_NumbersTheInvoiceAndSendsItAsTheFirstNotice()
    {
        // Arrange
        INotificationMessage sent = null;
        var context = new TestContextBuilder(CreateOutstanding());

        context.Services.AddSingleton<ITransactionReminderService>(CreateReminderService(message => sent = message, context.Clock));

        // Act
        var result = await context.Service.SendInvoiceAsync("transaction-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("INV-1001", result.Transaction.InvoiceNumber);
        Assert.Equal("Invoice INV-1001", sent.Subject);
        Assert.Contains("https://shop.example.com/transactions/pay/token", sent.TextBody, StringComparison.Ordinal);
        Assert.DoesNotContain("reminder", sent.Subject, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(TransactionEventType.InvoiceSent, result.Event.Type);
        Assert.Equal(1, result.Transaction.ReminderCount);
        Assert.Equal(_now, result.Transaction.LastReminderSentUtc);

        // It is the first notice, so the sweep does not send another one the same day.
        result.Transaction.DueUtc = _now.AddDays(-1);
        Assert.False(TransactionReminderBackgroundTask.IsReminderDue(result.Transaction, new TransactionReminderSettings(), _now));
    }

    [Fact]
    public async Task SendInvoiceAsync_WithoutTheRemindersFeature_IsRefused()
    {
        // Arrange
        var context = new TestContextBuilder(CreateOutstanding());

        // Act
        var result = await context.Service.SendInvoiceAsync("transaction-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Null(result.Transaction.InvoiceNumber);
    }

    [Fact]
    public async Task RefundAsync_RefundsTheNewestPaymentFirstUpToTheAmount()
    {
        // Arrange
        var paid = CreateOutstanding();
        paid.AmountPaid = 100m;
        paid.Status = TransactionStatus.Paid;

        var context = new TestContextBuilder(paid);
        var older = CreateAttempt("attempt-old", 60m, _now.AddDays(-5));
        var newer = CreateAttempt("attempt-new", 40m, _now.AddDays(-1));
        var refunds = context.UseRefunds(older, newer);

        // Act
        var result = await context.Service.RefundAsync("transaction-1", 50m, "Session canceled", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Collection(
            refunds,
            first =>
            {
                Assert.Equal("pi_attempt-new", first.OriginalTransactionId);
                Assert.Equal(40m, first.Amount);
                Assert.Equal("Session canceled", first.Reason);
            },
            second =>
            {
                Assert.Equal("pi_attempt-old", second.OriginalTransactionId);
                Assert.Equal(10m, second.Amount);
            });
        Assert.Equal(2, result.Refunds.Count);
    }

    [Fact]
    public async Task RefundAsync_ForMoreThanWasPaid_RefundsNothing()
    {
        // Arrange
        var paid = CreateOutstanding();
        paid.AmountPaid = 100m;
        paid.Status = TransactionStatus.Paid;

        var context = new TestContextBuilder(paid);
        var refunds = context.UseRefunds(CreateAttempt("attempt-1", 100m, _now));

        // Act
        var result = await context.Service.RefundAsync("transaction-1", 150m, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Empty(refunds);
    }

    [Fact]
    public async Task Command_WhileAnotherCommandHoldsTheTransaction_IsRefused()
    {
        // Arrange
        var context = new TestContextBuilder(CreateOutstanding());
        var (locker, locked) = await context.Lock.TryAcquireLockAsync(DefaultTransactionCommandService.LockPrefix + "transaction-1", TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1));

        Assert.True(locked);

        await using var _ = locker;

        // Act
        var result = await context.Service.AdjustAsync("transaction-1", 10m, TransactionAdjustmentKind.Discount, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(100m, (await context.Store.FindByIdAsync("transaction-1", TestContext.Current.CancellationToken)).OutstandingAmount);
    }

    [Fact]
    public async Task InvoiceBuilder_ShowsEachAdjustmentAndOnlyWhatIsStillOwed()
    {
        // Arrange
        var context = new TestContextBuilder(CreateOutstanding());

        await context.Service.AdjustAsync("transaction-1", 30m, TransactionAdjustmentKind.Waiver, "Financial aid", TestContext.Current.CancellationToken);

        var transaction = await context.Manager.FindByIdAsync("transaction-1", TestContext.Current.CancellationToken);
        var builder = new TransactionInvoiceBuilder(CreateReceiptService(), Mock.Of<ICustomerContactResolver>(), null, new PassThroughStringLocalizer<TransactionInvoiceBuilder>());

        // Act
        var invoice = await builder.BuildAsync(transaction, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(70m, invoice.Total);
        Assert.Contains(invoice.LineItems, line => line.Amount == -30m && line.Description.Contains("Financial aid", StringComparison.Ordinal));
        Assert.Equal(invoice.Subtotal, invoice.LineItems.Sum(line => line.Amount));
        Assert.Equal(ReceiptStatus.Due, invoice.Status);
    }

    private static Transaction CreateOutstanding()
        => new()
        {
            ItemId = "transaction-1",
            OwnerId = "customer-1",
            OwnerKind = CustomerOwnerKind.Authenticated,
            Title = "Fall registration",
            Currency = "USD",
            Amount = 100m,
            TotalAmount = 100m,
            Status = TransactionStatus.Outstanding,
            CreatedUtc = _now.AddDays(-10),
            UpdatedUtc = _now.AddDays(-10),
        };

    private static PaymentAttempt CreateAttempt(string itemId, decimal amount, DateTime createdUtc)
        => new()
        {
            ItemId = itemId,
            SessionId = "session-" + itemId,
            ProviderKey = "Stripe",
            ReferenceType = TransactionsConstants.ReferenceTypes.Transaction,
            ReferenceId = "transaction-1",
            State = PaymentAttemptState.Succeeded,
            TransactionId = "pi_" + itemId,
            Currency = "USD",
            ConfirmedAmount = amount,
            CreatedUtc = createdUtc,
        };

    private static IReceiptService CreateReceiptService()
    {
        var receipts = new Mock<IReceiptService>();
        receipts
            .Setup(service => service.BuildAsync(It.IsAny<ReceiptRequest>()))
            .ReturnsAsync((ReceiptRequest request) => new ReceiptDocument
            {
                Reference = request.Reference,
                Currency = request.Currency,
                LineItems = request.LineItems,
                TaxLines = request.TaxLines,
                TaxAmount = request.TaxAmount,
                Total = request.Total,
                Subtotal = request.Total - request.TaxAmount,
                Status = request.Status,
                Notes = request.Notes,
            });

        return receipts.Object;
    }

    private static DefaultTransactionReminderService CreateReminderService(Action<INotificationMessage> onSend, TestClock clock)
    {
        var userService = new Mock<IUserService>();
        userService.Setup(candidate => candidate.GetUserByUniqueIdAsync("customer-1")).ReturnsAsync(Mock.Of<IUser>());

        var notifications = new Mock<INotificationService>();
        notifications
            .Setup(candidate => candidate.SendAsync(It.IsAny<object>(), It.IsAny<INotificationMessage>(), It.IsAny<CancellationToken>()))
            .Callback((object _, INotificationMessage message, CancellationToken _) => onSend(message))
            .ReturnsAsync(new NotificationSendResult { SuccessfulCount = 1 });

        var payLinks = new Mock<ITransactionPayLinkService>();
        payLinks
            .Setup(candidate => candidate.CreatePayUrlAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://shop.example.com/transactions/pay/token");

        var numbers = new Mock<IFinancialDocumentNumberGenerator>();
        numbers
            .Setup(candidate => candidate.GenerateAsync(It.Is<FinancialDocumentNumberRequest>(request => request.Kind == FinancialDocumentKind.Invoice), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinancialDocumentNumber(1001, "INV-1001"));

        var services = new ServiceCollection()
            .AddSingleton<ITransactionInvoiceService>(new TransactionInvoiceService(numbers.Object))
            .AddSingleton(payLinks.Object);

        return new DefaultTransactionReminderService(
            TestCustomerOwnerNotifier.Create(notifications.Object, userService.Object, Mock.Of<ICustomerContactResolver>(), services.BuildServiceProvider()),
            services.BuildServiceProvider(),
            clock,
            NullLogger<DefaultTransactionReminderService>.Instance,
            new PassThroughStringLocalizer<DefaultTransactionReminderService>());
    }

    private sealed class TestContextBuilder
    {
        private DefaultTransactionCommandService _service;

        public TestContextBuilder(params Transaction[] transactions)
        {
            Store = new FakeTransactionStore(transactions);
            Manager = TransactionManagerFactory.Resolve(Store, Events);

            var payments = new Mock<ITransactionPaymentHandler>();
            payments
                .Setup(handler => handler.PaymentRecordedAsync(It.IsAny<TransactionPaymentRecordedContext>(), It.IsAny<CancellationToken>()))
                .Callback((TransactionPaymentRecordedContext context, CancellationToken _) => Payments.Add(context.Payment))
                .Returns(Task.CompletedTask);

            Services.AddSingleton(payments.Object);
        }

        public FakeTransactionStore Store { get; }

        public RecordingTransactionEventHandler Events { get; } = new();

        public List<TransactionEvent> Payments { get; } = [];

        public ITransactionManager Manager { get; }

        public TestClock Clock { get; } = new(_now);

        public LocalLock Lock { get; } = new(NullLogger<LocalLock>.Instance);

        public ServiceCollection Services { get; } = new();

        // Built on first use, so a test can register the services a command needs before it runs one.
        public DefaultTransactionCommandService Service
            => _service ??= TransactionCommandServiceFactory.Create(Manager, Clock, Services.BuildServiceProvider(), Lock);

        // The confirmed attempts paid for the transaction, and a refund service that records each refund it is asked for
        // and reports it succeeded.
        public List<RequestPaymentRefundContext> UseRefunds(params PaymentAttempt[] attempts)
        {
            var requested = new List<RequestPaymentRefundContext>();
            var refundService = new Mock<ICheckoutRefundService>();
            refundService
                .Setup(service => service.RequestRefundAsync(It.IsAny<RequestPaymentRefundContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((RequestPaymentRefundContext request, CancellationToken _) =>
                {
                    requested.Add(request);

                    return new PaymentRefund
                    {
                        ItemId = "refund-" + requested.Count,
                        OriginalTransactionId = request.OriginalTransactionId,
                        RefundGrossAmount = request.Amount ?? 0m,
                        Currency = "USD",
                        Status = RefundStatus.Succeeded,
                    };
                });

            Services.AddSingleton(refundService.Object);
            Services.AddSingleton<IPaymentAttemptStore>(new InMemoryPaymentAttemptStore(attempts));
            Services.AddSingleton<IPaymentRefundStore>(new InMemoryPaymentRefundStore());

            return requested;
        }
    }
}
