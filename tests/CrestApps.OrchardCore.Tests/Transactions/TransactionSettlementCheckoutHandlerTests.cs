using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Models;
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
    public async Task CompletedAsync_MarksTransactionPaidOnline()
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
        await handler.CompletedAsync(new CheckoutFlowCompletedContext(new CheckoutFlow(session)));

        // Assert
        var stored = await store.FindByIdAsync(transaction.ItemId, TestContext.Current.CancellationToken);
        Assert.Equal(TransactionStatus.Paid, stored.Status);
        Assert.Equal(stored.TotalAmount, stored.AmountPaid);
        Assert.Equal(TransactionsConstants.SettlementMethods.Online, stored.SettlementMethod);
        Assert.Equal("settlement-1", stored.SettlementReference);
        Assert.Contains(stored.Events, e => e.Type == TransactionEventType.PaymentRecorded);
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

    private static TransactionSettlementCheckoutHandler CreateHandler(FakeTransactionStore store)
        => new(
            TransactionManagerFactory.Create(store),
            new TestClock(_now),
            NullLogger<TransactionSettlementCheckoutHandler>.Instance,
            new PassThroughStringLocalizer<TransactionSettlementCheckoutHandler>());
}
