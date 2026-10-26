using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Tests.Checkout;
using CrestApps.OrchardCore.Tests.Checkout.Fakes;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Transactions;

/// <summary>
/// A refund of a payment that settled several transactions is recorded per transaction: on the one it was started from,
/// or, for a refund of the whole payment, split back over them.
/// </summary>
public sealed class TransactionRefundHandlerSharedPaymentTests
{
    private static readonly DateTime _now = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task RefundSucceededAsync_ForOneOfTheTransactions_IsRecordedOnThatOneOnly()
    {
        // Arrange
        var (handler, store) = CreateHandler();

        // Act
        await handler.RefundSucceededAsync(CreateRefund(50m, referenceId: "transaction-2"), TestContext.Current.CancellationToken);

        // Assert
        var first = await store.FindByIdAsync("transaction-1", TestContext.Current.CancellationToken);
        var second = await store.FindByIdAsync("transaction-2", TestContext.Current.CancellationToken);

        Assert.Equal(0m, first.AmountRefunded);
        Assert.Equal(50m, second.AmountRefunded);
        Assert.Equal(TransactionStatus.Refunded, second.Status);
        Assert.Equal(TransactionStatus.Paid, first.Status);
    }

    [Fact]
    public async Task RefundSucceededAsync_OfTheWholePayment_IsSplitBackOverTheTransactions()
    {
        // Arrange: a refund made from the payments page, for no transaction in particular.
        var (handler, store) = CreateHandler();

        // Act
        await handler.RefundSucceededAsync(CreateRefund(120m, referenceId: null), TestContext.Current.CancellationToken);

        // Assert
        var first = await store.FindByIdAsync("transaction-1", TestContext.Current.CancellationToken);
        var second = await store.FindByIdAsync("transaction-2", TestContext.Current.CancellationToken);

        Assert.Equal(108m, first.AmountRefunded);
        Assert.Equal(TransactionStatus.Refunded, first.Status);
        Assert.Equal(12m, second.AmountRefunded);
        Assert.Equal(TransactionStatus.Paid, second.Status);
    }

    [Fact]
    public async Task RefundSucceededAsync_ReportedTwice_IsRecordedOnce()
    {
        // Arrange
        var (handler, store) = CreateHandler();
        var refund = CreateRefund(120m, referenceId: null);

        // Act
        await handler.RefundSucceededAsync(refund, TestContext.Current.CancellationToken);
        await handler.RefundSucceededAsync(refund, TestContext.Current.CancellationToken);

        // Assert
        var first = await store.FindByIdAsync("transaction-1", TestContext.Current.CancellationToken);
        var second = await store.FindByIdAsync("transaction-2", TestContext.Current.CancellationToken);

        Assert.Equal(108m, first.AmountRefunded);
        Assert.Equal(12m, second.AmountRefunded);
    }

    private static PaymentRefund CreateRefund(decimal amount, string referenceId)
        => new()
        {
            ItemId = "refund-1",
            SessionId = "session-1",
            OriginalAttemptId = "attempt-1",
            OriginalTransactionId = "pi_attempt-1",
            ReferenceType = referenceId is null ? null : TransactionsConstants.ReferenceTypes.Transaction,
            ReferenceId = referenceId,
            Currency = "USD",
            RefundGrossAmount = amount,
            Status = RefundStatus.Succeeded,
        };

    // Two transactions settled together by one payment of 158: 108 on the first and 50 on the second.
    private static (TransactionRefundHandler Handler, FakeTransactionStore Store) CreateHandler()
    {
        var store = new FakeTransactionStore(CreatePaid("transaction-1", 108m), CreatePaid("transaction-2", 50m));

        var sessions = new InMemoryCheckoutSessionStore();
        var session = new CheckoutSession
        {
            SessionId = "session-1",
            ReferenceType = TransactionsConstants.ReferenceTypes.Transaction,
            Status = CheckoutSessionStatus.Completed,
        };

        session.References.Add(new CheckoutReference { ReferenceType = TransactionsConstants.ReferenceTypes.Transaction, ReferenceId = "transaction-1" });
        session.References.Add(new CheckoutReference { ReferenceType = TransactionsConstants.ReferenceTypes.Transaction, ReferenceId = "transaction-2" });
        sessions.Seed(session);

        var attempts = new InMemoryPaymentAttemptStore(new PaymentAttempt
        {
            ItemId = "attempt-1",
            SessionId = "session-1",
            ReferenceType = TransactionsConstants.ReferenceTypes.Transaction,
            State = PaymentAttemptState.Succeeded,
            TransactionId = "pi_attempt-1",
            Currency = "USD",
            ConfirmedAmount = 158m,
        });

        var services = new ServiceCollection()
            .AddSingleton<IPaymentAttemptStore>(attempts)
            .AddSingleton<ICheckoutSessionStore>(sessions)
            .BuildServiceProvider();

        var handler = new TransactionRefundHandler(
            TransactionManagerFactory.Create(store),
            services,
            new TestClock(_now),
            new PassThroughStringLocalizer<TransactionRefundHandler>());

        return (handler, store);
    }

    private static Transaction CreatePaid(string itemId, decimal total)
    {
        var transaction = new Transaction
        {
            ItemId = itemId,
            OwnerId = "customer-1",
            Title = "Fee " + itemId,
            Currency = "USD",
            Amount = total,
            TotalAmount = total,
            AmountPaid = total,
            Status = TransactionStatus.Paid,
            CreatedUtc = _now,
            UpdatedUtc = _now,
        };

        transaction.Events.Add(new TransactionEvent
        {
            Id = "payment-" + itemId,
            Type = TransactionEventType.PaymentRecorded,
            PaymentAttemptId = "attempt-1",
            Amount = total,
        });

        return transaction;
    }
}
