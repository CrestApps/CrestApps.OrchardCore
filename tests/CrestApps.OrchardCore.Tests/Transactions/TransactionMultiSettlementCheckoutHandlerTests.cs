using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Tests.Checkout;
using CrestApps.OrchardCore.Tests.Checkout.Fakes;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Locking.Distributed;
using Xunit;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Transactions;

/// <summary>
/// One checkout settles several outstanding transactions of one customer with one payment: each is a line of the
/// checkout, and the payment is split back so each transaction records its own share.
/// </summary>
public sealed class TransactionMultiSettlementCheckoutHandlerTests
{
    private static readonly DateTime _now = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task StartAsync_WithSeveralTransactions_ListsEachAsALineAndOffersNoPromotionCode()
    {
        // Arrange
        var first = CreateTransaction("transaction-1", 108m);
        var second = CreateTransaction("transaction-2", 50m);
        var (engine, _) = CreateEngine(new FakeTransactionStore(first, second));

        // Act
        var session = await engine.StartAsync(CreateRequest("transaction-1", "transaction-2"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TransactionsConstants.ReferenceTypes.Transaction, session.ReferenceType);
        Assert.Null(session.ReferenceId);
        Assert.Equal(["transaction-1", "transaction-2"], session.References.Select(reference => reference.ReferenceId));
        Assert.Equal("USD", session.Currency);
        Assert.True(session.SettlesExistingBalance);
        Assert.False(session.AcceptsDiscountsOnBalance);

        var step = Assert.Single(session.Steps);
        Assert.Equal(TransactionsConstants.SettlementStepKey, step.Key);
        Assert.Collection(
            step.BillingItems,
            item =>
            {
                Assert.Equal("transaction-1", item.ItemId);
                Assert.Equal(108m, item.Amount);
                Assert.True(item.ExcludeFromTax);
            },
            item =>
            {
                Assert.Equal("transaction-2", item.ItemId);
                Assert.Equal(50m, item.Amount);
                Assert.True(item.ExcludeFromTax);
            });
    }

    [Fact]
    public async Task StartAsync_WithOneTransactionInTheList_StartsTheSingleTransactionCheckout()
    {
        // Arrange
        var (engine, _) = CreateEngine(new FakeTransactionStore(CreateTransaction("transaction-1", 108m)));

        // Act
        var session = await engine.StartAsync(CreateRequest("transaction-1"), TestContext.Current.CancellationToken);

        // Assert: exactly the checkout a caller naming the transaction directly gets, a promotion code included.
        Assert.Equal("transaction-1", session.ReferenceId);
        Assert.Empty(session.References);
        Assert.True(session.AcceptsDiscountsOnBalance);
        Assert.Equal("transaction-1", Assert.Single(Assert.Single(session.Steps).BillingItems).ItemId);
    }

    [Fact]
    public async Task StartAsync_WithTransactionsOfDifferentCustomers_IsRejected()
    {
        // Arrange
        var other = CreateTransaction("transaction-2", 50m);
        other.OwnerId = "customer-2";

        var (engine, sessions) = CreateEngine(new FakeTransactionStore(CreateTransaction("transaction-1", 108m), other));

        // Act
        var exception = await Assert.ThrowsAsync<CheckoutRejectedException>(() => engine.StartAsync(CreateRequest("transaction-1", "transaction-2"), TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains("Transactions of different customers can't be paid together.", exception.Errors);
        Assert.Equal(0, sessions.SaveCount);
    }

    [Fact]
    public async Task StartAsync_WithTransactionsInDifferentCurrencies_IsRejected()
    {
        // Arrange
        var other = CreateTransaction("transaction-2", 50m);
        other.Currency = "EUR";

        var (engine, sessions) = CreateEngine(new FakeTransactionStore(CreateTransaction("transaction-1", 108m), other));

        // Act
        var exception = await Assert.ThrowsAsync<CheckoutRejectedException>(() => engine.StartAsync(CreateRequest("transaction-1", "transaction-2"), TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains("Transactions in different currencies can't be paid together.", exception.Errors);
        Assert.Equal(0, sessions.SaveCount);
    }

    [Fact]
    public async Task StartAsync_WithATransactionThatIsAlreadyPaid_IsRejected()
    {
        // Arrange
        var paid = CreateTransaction("transaction-2", 50m);
        paid.AmountPaid = 50m;
        paid.Status = TransactionStatus.Paid;

        var (engine, _) = CreateEngine(new FakeTransactionStore(CreateTransaction("transaction-1", 108m), paid));

        // Act
        var exception = await Assert.ThrowsAsync<CheckoutRejectedException>(() => engine.StartAsync(CreateRequest("transaction-1", "transaction-2"), TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains("Nothing is left to pay on Fee transaction-2.", exception.Errors);
    }

    [Fact]
    public async Task StartAsync_WithATransactionThatCanNoLongerBePaid_IsRejected()
    {
        // Arrange
        var canceled = CreateTransaction("transaction-2", 50m);
        canceled.Status = TransactionStatus.Canceled;

        var (engine, _) = CreateEngine(new FakeTransactionStore(CreateTransaction("transaction-1", 108m), canceled));

        // Act
        var exception = await Assert.ThrowsAsync<CheckoutRejectedException>(() => engine.StartAsync(CreateRequest("transaction-1", "transaction-2"), TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains("Fee transaction-2 can no longer be paid.", exception.Errors);
    }

    [Fact]
    public async Task StartAsync_WithATransactionThatDoesNotExist_IsRejected()
    {
        // Arrange
        var (engine, _) = CreateEngine(new FakeTransactionStore(CreateTransaction("transaction-1", 108m)));

        // Act
        var exception = await Assert.ThrowsAsync<CheckoutRejectedException>(() => engine.StartAsync(CreateRequest("transaction-1", "missing"), TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains("A transaction to pay could not be found.", exception.Errors);
    }

    [Fact]
    public async Task CompletedAsync_SplitsTheOnePaymentAcrossTheTransactions()
    {
        // Arrange
        var store = new FakeTransactionStore(CreateTransaction("transaction-1", 108m), CreateTransaction("transaction-2", 50m));
        var attempts = new InMemoryPaymentAttemptStore(CreateAttempt("attempt-1", 158m));
        var recorder = new RecordingPaymentHandler();
        var handler = CreateHandler(store, attempts, recorder);
        var session = await StartAsync(handler, "transaction-1", "transaction-2");

        // Act
        await handler.CompletedAsync(new CheckoutFlowCompletedContext(new CheckoutFlow(session)));

        // Assert
        var first = await store.FindByIdAsync("transaction-1", TestContext.Current.CancellationToken);
        var second = await store.FindByIdAsync("transaction-2", TestContext.Current.CancellationToken);

        Assert.Equal(TransactionStatus.Paid, first.Status);
        Assert.Equal(108m, first.AmountPaid);
        Assert.Equal(TransactionStatus.Paid, second.Status);
        Assert.Equal(50m, second.AmountPaid);

        var firstPayment = Assert.Single(first.Events, evt => evt.Type == TransactionEventType.PaymentRecorded);
        var secondPayment = Assert.Single(second.Events, evt => evt.Type == TransactionEventType.PaymentRecorded);

        Assert.Equal("attempt-1", firstPayment.PaymentAttemptId);
        Assert.Equal(108m, firstPayment.Amount);
        Assert.Equal("attempt-1", secondPayment.PaymentAttemptId);
        Assert.Equal(50m, secondPayment.Amount);
        Assert.Equal(session.SessionId, first.SettlementReference);
        Assert.Equal(session.SessionId, second.SettlementReference);

        // Each transaction tells its payment handlers about its own share, so each gets a receipt of its own.
        Assert.Equal(["transaction-1", "transaction-2"], recorder.Contexts.Select(context => context.Transaction.ItemId));
    }

    [Fact]
    public async Task CompletedAsync_WhenTheGatewayConfirmedLess_PaysTheTransactionsInOrderAndLeavesTheRestOwing()
    {
        // Arrange
        var store = new FakeTransactionStore(CreateTransaction("transaction-1", 108m), CreateTransaction("transaction-2", 50m));
        var handler = CreateHandler(store, new InMemoryPaymentAttemptStore(CreateAttempt("attempt-1", 120m)));
        var session = await StartAsync(handler, "transaction-1", "transaction-2");

        // Act
        await handler.CompletedAsync(new CheckoutFlowCompletedContext(new CheckoutFlow(session)));

        // Assert
        var first = await store.FindByIdAsync("transaction-1", TestContext.Current.CancellationToken);
        var second = await store.FindByIdAsync("transaction-2", TestContext.Current.CancellationToken);

        Assert.Equal(TransactionStatus.Paid, first.Status);
        Assert.Equal(TransactionStatus.PartiallyPaid, second.Status);
        Assert.Equal(12m, second.AmountPaid);
        Assert.Equal(38m, second.OutstandingAmount);
    }

    [Fact]
    public async Task CompletedAsync_RunTwice_AppliesThePaymentOnce()
    {
        // Arrange
        var store = new FakeTransactionStore(CreateTransaction("transaction-1", 108m), CreateTransaction("transaction-2", 50m));
        var handler = CreateHandler(store, new InMemoryPaymentAttemptStore(CreateAttempt("attempt-1", 158m)));
        var session = await StartAsync(handler, "transaction-1", "transaction-2");

        // Act
        await handler.CompletedAsync(new CheckoutFlowCompletedContext(new CheckoutFlow(session)));
        await handler.CompletedAsync(new CheckoutFlowCompletedContext(new CheckoutFlow(session)));

        // Assert
        var first = await store.FindByIdAsync("transaction-1", TestContext.Current.CancellationToken);
        var second = await store.FindByIdAsync("transaction-2", TestContext.Current.CancellationToken);

        Assert.Equal(108m, first.AmountPaid);
        Assert.Equal(50m, second.AmountPaid);
        Assert.Single(first.Events, evt => evt.Type == TransactionEventType.PaymentRecorded);
        Assert.Single(second.Events, evt => evt.Type == TransactionEventType.PaymentRecorded);
    }

    [Fact]
    public async Task CompletedAsync_WithoutAConfirmedPayment_SettlesNothing()
    {
        // Arrange
        var store = new FakeTransactionStore(CreateTransaction("transaction-1", 108m), CreateTransaction("transaction-2", 50m));
        var pending = CreateAttempt("attempt-1", 0m);
        pending.State = PaymentAttemptState.Pending;

        var handler = CreateHandler(store, new InMemoryPaymentAttemptStore(pending));
        var session = await StartAsync(handler, "transaction-1", "transaction-2");

        // Act
        await handler.CompletedAsync(new CheckoutFlowCompletedContext(new CheckoutFlow(session)));

        // Assert
        Assert.All(store.Transactions, transaction => Assert.Equal(0m, transaction.AmountPaid));
    }

    // The handler activates the session the way the checkout does, so the step it completes against is its own.
    private static async Task<CheckoutSession> StartAsync(TransactionSettlementCheckoutHandler handler, params string[] transactionIds)
    {
        var store = new InMemoryCheckoutSessionStore([handler]);

        return await store.NewAsync(
            TransactionsConstants.ReferenceTypes.Transaction,
            referenceId: null,
            configure: session =>
            {
                foreach (var transactionId in transactionIds)
                {
                    session.References.Add(new CheckoutReference
                    {
                        ReferenceType = TransactionsConstants.ReferenceTypes.Transaction,
                        ReferenceId = transactionId,
                    });
                }
            },
            cancellationToken: TestContext.Current.CancellationToken);
    }

    private static StartCheckoutRequest CreateRequest(params string[] transactionIds)
        => new()
        {
            References = [.. transactionIds.Select(transactionId => new CheckoutReference
            {
                ReferenceType = TransactionsConstants.ReferenceTypes.Transaction,
                ReferenceId = transactionId,
            })],
        };

    private static Transaction CreateTransaction(string itemId, decimal total)
        => new()
        {
            ItemId = itemId,
            OwnerId = "customer-1",
            OwnerKind = CustomerOwnerKind.Authenticated,
            Title = "Fee " + itemId,
            Currency = "USD",
            Amount = total,
            TotalAmount = total,
            Status = TransactionStatus.Outstanding,
            CreatedUtc = _now,
            UpdatedUtc = _now,
        };

    private static PaymentAttempt CreateAttempt(string itemId, decimal amount)
        => new()
        {
            ItemId = itemId,
            SessionId = "session-1",
            ReferenceType = TransactionsConstants.ReferenceTypes.Transaction,
            State = PaymentAttemptState.Succeeded,
            TransactionId = "pi_" + itemId,
            Currency = "USD",
            ConfirmedAmount = amount,
            CreatedUtc = _now,
        };

    private static (DefaultCheckoutEngine Engine, InMemoryCheckoutSessionStore Sessions) CreateEngine(FakeTransactionStore store)
    {
        var sessions = new InMemoryCheckoutSessionStore([CreateHandler(store, new InMemoryPaymentAttemptStore())]);
        var attemptStore = new InMemoryPaymentAttemptStore();
        var providerResolver = Mock.Of<ICheckoutPaymentProviderResolver>();

        var engine = new DefaultCheckoutEngine(
            sessions,
            attemptStore,
            providerResolver,
            Mock.Of<ICheckoutRecurringPaymentProviderResolver>(),
            new CheckoutReconciliationService(attemptStore, providerResolver, NullLogger<CheckoutReconciliationService>.Instance),
            Mock.Of<ICheckoutRefundService>(),
            [],
            Mock.Of<IDistributedLock>(),
            Mock.Of<ISession>(),
            new TestClock(_now),
            NullLogger<DefaultCheckoutEngine>.Instance);

        return (engine, sessions);
    }

    private static TransactionSettlementCheckoutHandler CreateHandler(FakeTransactionStore store, InMemoryPaymentAttemptStore attempts, params ITransactionPaymentHandler[] paymentHandlers)
    {
        var services = new ServiceCollection();

        foreach (var paymentHandler in paymentHandlers)
        {
            services.AddSingleton(paymentHandler);
        }

        var serviceProvider = services.BuildServiceProvider();
        var manager = TransactionManagerFactory.Create(store);
        var clock = new TestClock(_now);

        return new(
            manager,
            attempts,
            new TransactionSettlementService(
                manager,
                serviceProvider,
                clock,
                NullLogger<TransactionSettlementService>.Instance,
                new PassThroughStringLocalizer<TransactionSettlementService>()),
            new CheckoutPaymentProviderResolver([]),
            serviceProvider,
            NullLogger<TransactionSettlementCheckoutHandler>.Instance,
            new PassThroughStringLocalizer<TransactionSettlementCheckoutHandler>());
    }

    private sealed class RecordingPaymentHandler : ITransactionPaymentHandler
    {
        public List<TransactionPaymentRecordedContext> Contexts { get; } = [];

        public Task PaymentRecordedAsync(TransactionPaymentRecordedContext context, CancellationToken cancellationToken = default)
        {
            Contexts.Add(context);

            return Task.CompletedTask;
        }
    }
}
