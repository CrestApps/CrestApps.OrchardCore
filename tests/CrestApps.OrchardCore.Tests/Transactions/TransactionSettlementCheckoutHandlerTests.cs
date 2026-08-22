using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Tests.Checkout;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Transactions;

public sealed class TransactionSettlementCheckoutHandlerTests
{
    private static readonly DateTime _now = new(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ActivatingAsync_AddsOutstandingBalanceAsBillingItem()
    {
        // Arrange
        var transaction = CreateOutstandingTransaction();
        var store = new FakeTransactionStore(transaction);
        var handler = CreateHandler(store);

        var session = new CheckoutSession
        {
            SessionId = "settlement-1",
            ReferenceType = TransactionsConstants.ReferenceTypes.Transaction,
            ReferenceId = transaction.ItemId,
            Status = CheckoutSessionStatus.Pending,
        };

        // Act
        await handler.ActivatingAsync(new CheckoutFlowActivatingContext(session));

        // Assert
        var step = Assert.Single(session.Steps);
        var billingItem = Assert.Single(step.BillingItems);
        Assert.Equal(transaction.ItemId, billingItem.ItemId);
        Assert.Equal(transaction.OutstandingAmount, billingItem.Amount);
        Assert.Equal("USD", session.Currency);
    }

    [Fact]
    public async Task CompletedAsync_SettlesAgainstConfirmedPaymentAmount()
    {
        // Arrange
        var transaction = CreateOutstandingTransaction();
        var store = new FakeTransactionStore(transaction);

        var attempts = new InMemoryPaymentAttemptStore(new PaymentAttempt
        {
            SessionId = "settlement-1",
            Currency = "USD",
            State = PaymentAttemptState.Succeeded,
            ConfirmedAmount = 100m,
            ConfirmedTaxAmount = 8m,
        });

        var handler = CreateHandler(store, attempts);
        var session = CreateSettlementSession(transaction, "settlement-1");

        // Act
        await handler.CompletedAsync(new CheckoutFlowCompletedContext(new CheckoutFlow(session)));

        // Assert
        var stored = await store.FindByIdAsync(transaction.ItemId, TestContext.Current.CancellationToken);
        Assert.Equal(TransactionStatus.Paid, stored.Status);
        Assert.Equal(108m, stored.AmountPaid);
        Assert.Equal(TransactionsConstants.SettlementMethods.Online, stored.SettlementMethod);
        Assert.Equal("settlement-1", stored.SettlementReference);
        Assert.False(string.IsNullOrEmpty(stored.PaymentAttemptId));
        Assert.Contains(stored.Events, e => e.Type == TransactionEventType.PaymentRecorded);
    }

    [Fact]
    public async Task CompletedAsync_LeavesTransactionUnsettledWhenNoConfirmedAttemptExists()
    {
        // Arrange
        var transaction = CreateOutstandingTransaction();
        var store = new FakeTransactionStore(transaction);

        var attempts = new InMemoryPaymentAttemptStore(new PaymentAttempt
        {
            SessionId = "settlement-1",
            Currency = "USD",
            State = PaymentAttemptState.Pending,
            ConfirmedAmount = 0m,
        });

        var handler = CreateHandler(store, attempts);
        var session = CreateSettlementSession(transaction, "settlement-1");

        // Act
        await handler.CompletedAsync(new CheckoutFlowCompletedContext(new CheckoutFlow(session)));

        // Assert
        var stored = await store.FindByIdAsync(transaction.ItemId, TestContext.Current.CancellationToken);
        Assert.Equal(TransactionStatus.Outstanding, stored.Status);
        Assert.Equal(0m, stored.AmountPaid);
        Assert.Null(stored.PaymentAttemptId);
    }

    [Fact]
    public async Task CompletedAsync_RefusesToSettleWhenCurrencyDiffers()
    {
        // Arrange
        var transaction = CreateOutstandingTransaction();
        var store = new FakeTransactionStore(transaction);

        var attempts = new InMemoryPaymentAttemptStore(new PaymentAttempt
        {
            SessionId = "settlement-1",
            Currency = "EUR",
            State = PaymentAttemptState.Succeeded,
            ConfirmedAmount = 100m,
            ConfirmedTaxAmount = 8m,
        });

        var handler = CreateHandler(store, attempts);
        var session = CreateSettlementSession(transaction, "settlement-1");

        // Act
        await handler.CompletedAsync(new CheckoutFlowCompletedContext(new CheckoutFlow(session)));

        // Assert
        var stored = await store.FindByIdAsync(transaction.ItemId, TestContext.Current.CancellationToken);
        Assert.Equal(TransactionStatus.Outstanding, stored.Status);
        Assert.Equal(0m, stored.AmountPaid);
    }

    [Fact]
    public async Task CompletedAsync_MarksTransactionPartiallyPaidWhenConfirmedAmountIsLower()
    {
        // Arrange
        var transaction = CreateOutstandingTransaction();
        var store = new FakeTransactionStore(transaction);

        var attempts = new InMemoryPaymentAttemptStore(new PaymentAttempt
        {
            SessionId = "settlement-1",
            Currency = "USD",
            State = PaymentAttemptState.Succeeded,
            ConfirmedAmount = 40m,
            ConfirmedTaxAmount = 0m,
        });

        var handler = CreateHandler(store, attempts);
        var session = CreateSettlementSession(transaction, "settlement-1");

        // Act
        await handler.CompletedAsync(new CheckoutFlowCompletedContext(new CheckoutFlow(session)));

        // Assert
        var stored = await store.FindByIdAsync(transaction.ItemId, TestContext.Current.CancellationToken);
        Assert.Equal(TransactionStatus.PartiallyPaid, stored.Status);
        Assert.Equal(40m, stored.AmountPaid);
        Assert.Null(stored.SettledUtc);
    }

    [Fact]
    public async Task CompletedAsync_IsIdempotentForTheSameSession()
    {
        // Arrange
        var transaction = CreateOutstandingTransaction();
        var store = new FakeTransactionStore(transaction);

        var attempts = new InMemoryPaymentAttemptStore(new PaymentAttempt
        {
            SessionId = "settlement-1",
            Currency = "USD",
            State = PaymentAttemptState.Succeeded,
            ConfirmedAmount = 40m,
            ConfirmedTaxAmount = 0m,
        });

        var handler = CreateHandler(store, attempts);
        var session = CreateSettlementSession(transaction, "settlement-1");

        // Act
        await handler.CompletedAsync(new CheckoutFlowCompletedContext(new CheckoutFlow(session)));
        await handler.CompletedAsync(new CheckoutFlowCompletedContext(new CheckoutFlow(session)));

        // Assert
        var stored = await store.FindByIdAsync(transaction.ItemId, TestContext.Current.CancellationToken);
        Assert.Equal(40m, stored.AmountPaid);
        Assert.Single(stored.Events, e => e.Type == TransactionEventType.PaymentRecorded);
    }

    [Fact]
    public async Task CompletedAsync_IgnoresSessionsThatDoNotReferenceATransaction()
    {
        // Arrange
        var transaction = CreateOutstandingTransaction();
        var store = new FakeTransactionStore(transaction);
        var handler = CreateHandler(store);

        var session = new CheckoutSession
        {
            SessionId = "order-1",
            ReferenceType = "order",
            ReferenceId = transaction.ItemId,
            Status = CheckoutSessionStatus.Pending,
        };

        // Act
        await handler.CompletedAsync(new CheckoutFlowCompletedContext(new CheckoutFlow(session)));

        // Assert
        var stored = await store.FindByIdAsync(transaction.ItemId, TestContext.Current.CancellationToken);
        Assert.Equal(TransactionStatus.Outstanding, stored.Status);
    }

    [Fact]
    public async Task CompletedAsync_RefusesToSettleWhenAttemptCurrencyIsMissing()
    {
        // Arrange
        var transaction = CreateOutstandingTransaction();
        var store = new FakeTransactionStore(transaction);

        var attempts = new InMemoryPaymentAttemptStore(new PaymentAttempt
        {
            SessionId = "settlement-1",
            Currency = null,
            State = PaymentAttemptState.Succeeded,
            ConfirmedAmount = 100m,
            ConfirmedTaxAmount = 8m,
        });

        var handler = CreateHandler(store, attempts);
        var session = CreateSettlementSession(transaction, "settlement-1");

        // Act
        await handler.CompletedAsync(new CheckoutFlowCompletedContext(new CheckoutFlow(session)));

        // Assert
        var stored = await store.FindByIdAsync(transaction.ItemId, TestContext.Current.CancellationToken);
        Assert.Equal(TransactionStatus.Outstanding, stored.Status);
        Assert.Equal(0m, stored.AmountPaid);
    }

    [Fact]
    public async Task CompletedAsync_DoesNotDoubleApplyAReplayedPartialPaymentFromAnEarlierSession()
    {
        // Arrange
        var transaction = CreateOutstandingTransaction();
        var store = new FakeTransactionStore(transaction);

        var attempts = new InMemoryPaymentAttemptStore(
            new PaymentAttempt
            {
                ItemId = "attempt-1",
                SessionId = "settlement-1",
                Currency = "USD",
                State = PaymentAttemptState.Succeeded,
                ConfirmedAmount = 40m,
            },
            new PaymentAttempt
            {
                ItemId = "attempt-2",
                SessionId = "settlement-2",
                Currency = "USD",
                State = PaymentAttemptState.Succeeded,
                ConfirmedAmount = 60m,
            });

        var handler = CreateHandler(store, attempts);

        // Act
        await handler.CompletedAsync(new CheckoutFlowCompletedContext(new CheckoutFlow(CreateSettlementSession(transaction, "settlement-1"))));
        await handler.CompletedAsync(new CheckoutFlowCompletedContext(new CheckoutFlow(CreateSettlementSession(transaction, "settlement-2"))));

        // A stale replay of the first session arrives after the second session already ran.
        await handler.CompletedAsync(new CheckoutFlowCompletedContext(new CheckoutFlow(CreateSettlementSession(transaction, "settlement-1"))));

        // Assert
        var stored = await store.FindByIdAsync(transaction.ItemId, TestContext.Current.CancellationToken);
        Assert.Equal(100m, stored.AmountPaid);
        Assert.Equal(2, stored.Events.Count(e => e.Type == TransactionEventType.PaymentRecorded));
    }

    private static CheckoutSession CreateSettlementSession(Transaction transaction, string sessionId)
        => new()
        {
            SessionId = sessionId,
            ReferenceType = TransactionsConstants.ReferenceTypes.Transaction,
            ReferenceId = transaction.ItemId,
            Status = CheckoutSessionStatus.Pending,
        };

    private static Transaction CreateOutstandingTransaction()
        => new()
        {
            ItemId = "transaction-1",
            Title = "Outstanding order",
            Currency = "USD",
            Amount = 100m,
            TaxAmount = 8m,
            TotalAmount = 108m,
            AmountPaid = 0m,
            Status = TransactionStatus.Outstanding,
            CreatedUtc = _now,
            UpdatedUtc = _now,
        };

    private static TransactionSettlementCheckoutHandler CreateHandler(FakeTransactionStore store, InMemoryPaymentAttemptStore attempts = null)
        => new(
            TransactionManagerFactory.Create(store),
            attempts ?? new InMemoryPaymentAttemptStore(),
            new TestClock(_now),
            NullLogger<TransactionSettlementCheckoutHandler>.Instance,
            new PassThroughStringLocalizer<TransactionSettlementCheckoutHandler>());
}
