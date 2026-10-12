using CrestApps.OrchardCore.Tests.Checkout;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Transactions;

/// <summary>
/// A refund has to show on the transaction it gives money back for. Before, the transaction kept saying Paid after
/// the customer had their money back, and the Refunded status was never set.
/// </summary>
public sealed class TransactionRefundHandlerTests
{
    [Fact]
    public async Task RefundSucceeded_ForEverythingPaid_MarksTheTransactionRefunded()
    {
        // Arrange
        var store = new FakeTransactionStore(CreatePaidTransaction());
        var handler = CreateHandler(store);

        // Act
        await handler.RefundSucceededAsync(CreateRefund("refund-1", 108m), TestContext.Current.CancellationToken);

        // Assert
        var transaction = await store.FindByIdAsync("transaction-1", TestContext.Current.CancellationToken);

        Assert.Equal(TransactionStatus.Refunded, transaction.Status);
        Assert.Equal(108m, transaction.AmountRefunded);
        Assert.Equal(108m, transaction.AmountPaid);
        Assert.Contains(transaction.Events, evt => evt.Type == TransactionEventType.Refunded && evt.Id == "refund-1" && evt.Amount == 108m);
    }

    [Fact]
    public async Task RefundSucceeded_ForPartOfWhatWasPaid_KeepsTheTransactionPaid()
    {
        // Arrange
        var store = new FakeTransactionStore(CreatePaidTransaction());
        var handler = CreateHandler(store);

        // Act
        await handler.RefundSucceededAsync(CreateRefund("refund-1", 40m), TestContext.Current.CancellationToken);

        // Assert
        var transaction = await store.FindByIdAsync("transaction-1", TestContext.Current.CancellationToken);

        Assert.Equal(TransactionStatus.Paid, transaction.Status);
        Assert.Equal(40m, transaction.AmountRefunded);
        Assert.Equal(0m, transaction.OutstandingAmount);
    }

    /// <summary>
    /// A reconciliation can report a refund the refund service already reported; it must count once.
    /// </summary>
    [Fact]
    public async Task RefundSucceeded_ReportedTwice_CountsOnce()
    {
        // Arrange
        var store = new FakeTransactionStore(CreatePaidTransaction());
        var handler = CreateHandler(store);
        var refund = CreateRefund("refund-1", 40m);

        // Act
        await handler.RefundSucceededAsync(refund, TestContext.Current.CancellationToken);
        await handler.RefundSucceededAsync(refund, TestContext.Current.CancellationToken);

        // Assert
        var transaction = await store.FindByIdAsync("transaction-1", TestContext.Current.CancellationToken);

        Assert.Equal(40m, transaction.AmountRefunded);
        Assert.Single(transaction.Events, evt => evt.Type == TransactionEventType.Refunded);
    }

    [Fact]
    public async Task RefundSucceeded_ForAPurchase_FindsTheTransactionByItsObligation()
    {
        // Arrange: the payment paid a checkout, not a transaction, so the refund's session and obligation lead to it.
        var transaction = CreatePaidTransaction();
        transaction.CheckoutSessionId = "session-1";
        transaction.ObligationId = "one-time";

        var store = new FakeTransactionStore(transaction);
        var handler = CreateHandler(store, new PaymentAttempt
        {
            ItemId = "attempt-1",
            SessionId = "session-1",
            ReferenceType = "Order",
            ReferenceId = "order-1",
            State = PaymentAttemptState.Succeeded,
        });

        // Act
        await handler.RefundSucceededAsync(CreateRefund("refund-1", 108m), TestContext.Current.CancellationToken);

        // Assert
        var stored = await store.FindByIdAsync("transaction-1", TestContext.Current.CancellationToken);

        Assert.Equal(TransactionStatus.Refunded, stored.Status);
    }

    private static TransactionRefundHandler CreateHandler(FakeTransactionStore store, PaymentAttempt attempt = null)
    {
        var services = new ServiceCollection();

        services.AddSingleton<IPaymentAttemptStore>(new InMemoryPaymentAttemptStore(attempt ?? new PaymentAttempt
        {
            ItemId = "attempt-1",
            SessionId = "pay-link-1",
            ReferenceType = TransactionsConstants.ReferenceTypes.Transaction,
            ReferenceId = "transaction-1",
            State = PaymentAttemptState.Succeeded,
        }));

        return new TransactionRefundHandler(
            TransactionManagerFactory.Create(store),
            services.BuildServiceProvider(),
            new TestClock(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)),
            new PassThroughStringLocalizer<TransactionRefundHandler>());
    }

    private static Transaction CreatePaidTransaction()
        => new()
        {
            ItemId = "transaction-1",
            Currency = "USD",
            Amount = 100m,
            TaxAmount = 8m,
            TotalAmount = 108m,
            AmountPaid = 108m,
            Status = TransactionStatus.Paid,
        };

    private static PaymentRefund CreateRefund(string itemId, decimal amount)
        => new()
        {
            ItemId = itemId,
            SessionId = "session-1",
            ObligationId = "one-time",
            OriginalAttemptId = "attempt-1",
            ProviderKey = "stripe",
            Currency = "USD",
            RefundGrossAmount = amount,
            Status = RefundStatus.Succeeded,
        };
}
