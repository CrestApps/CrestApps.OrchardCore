using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Tests.Checkout.Fakes;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tests.Transactions;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// Drives the test payment provider through the real checkout engine, reconciliation, transaction settlement and
/// refund service, the same path <c>POST checkout/{id}/payment/begin</c> and <c>/status</c> take. These are the
/// guarantees a consumer's end-to-end suite relies on: paying with the test method completes the checkout and
/// settles the transaction, refunding it shows on the transaction, and the magic amounts reach the declined and
/// still-pending paths.
/// </summary>
public sealed class TestPaymentsEndToEndTests
{
    private const string SessionId = "session-1";
    private const string TransactionId = "transaction-1";

    private static readonly DateTime _now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task BeginThenStatus_CompletesTheCheckoutAndSettlesTheTransaction()
    {
        // Arrange
        var harness = new Harness(108m);

        // Act
        var begun = await harness.Engine.BeginPaymentAsync(SessionId, new BeginPaymentOptions { ProviderKey = TestCheckoutPaymentProvider.ProviderKey }, TestContext.Current.CancellationToken);
        var status = await harness.Engine.TryCompleteAsync(SessionId, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(begun.Succeeded);
        Assert.False(begun.RequiresAction);
        Assert.Equal(CheckoutCompletionStatus.Completed, status.Status);
        Assert.Equal(CheckoutSessionStatus.Completed, (await harness.SessionStore.GetAsync(SessionId, TestContext.Current.CancellationToken)).Status);

        var attempt = Assert.Single(await harness.AttemptStore.GetBySessionAsync(SessionId, TestContext.Current.CancellationToken));

        Assert.Equal(PaymentAttemptState.Succeeded, attempt.State);
        Assert.Equal(GatewayMode.Testing, attempt.GatewayMode);
        Assert.Equal(108m, attempt.ConfirmedAmount);

        var transaction = await harness.TransactionStore.FindByIdAsync(TransactionId, TestContext.Current.CancellationToken);

        Assert.Equal(TransactionStatus.Paid, transaction.Status);
        Assert.Equal(108m, transaction.AmountPaid);
        Assert.Equal(0m, transaction.OutstandingAmount);
    }

    [Fact]
    public async Task Refund_OfATestPayment_SucceedsAndFlowsBackToTheTransaction()
    {
        // Arrange
        var harness = new Harness(108m);

        await harness.Engine.BeginPaymentAsync(SessionId, new BeginPaymentOptions { ProviderKey = TestCheckoutPaymentProvider.ProviderKey }, TestContext.Current.CancellationToken);
        await harness.Engine.TryCompleteAsync(SessionId, TestContext.Current.CancellationToken);

        var attempt = Assert.Single(await harness.AttemptStore.GetBySessionAsync(SessionId, TestContext.Current.CancellationToken));

        // Act
        var refund = await harness.RefundService.RequestRefundAsync(
            new RequestPaymentRefundContext
            {
                SessionId = SessionId,
                OriginalTransactionId = attempt.TransactionId,
                Reason = "Testing refunds.",
            },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(RefundStatus.Succeeded, refund.Status);
        Assert.Equal(108m, refund.RefundGrossAmount);
        Assert.False(string.IsNullOrEmpty(refund.ProviderRefundReference));

        var transaction = await harness.TransactionStore.FindByIdAsync(TransactionId, TestContext.Current.CancellationToken);

        Assert.Equal(TransactionStatus.Refunded, transaction.Status);
        Assert.Equal(108m, transaction.AmountRefunded);
        Assert.Contains(transaction.Events, evt => evt.Type == TransactionEventType.Refunded && evt.Id == refund.ItemId);
    }

    [Fact]
    public async Task Begin_WhenTheAmountEndsIn02_IsDeclinedAndTheTransactionStaysOutstanding()
    {
        // Arrange
        var harness = new Harness(10.02m);

        // Act
        var begun = await harness.Engine.BeginPaymentAsync(SessionId, new BeginPaymentOptions { ProviderKey = TestCheckoutPaymentProvider.ProviderKey }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(begun.Succeeded);
        Assert.True(begun.Declined);

        var attempt = Assert.Single(await harness.AttemptStore.GetBySessionAsync(SessionId, TestContext.Current.CancellationToken));

        Assert.Equal(PaymentAttemptState.Failed, attempt.State);
        Assert.Equal(TransactionStatus.Outstanding, (await harness.TransactionStore.FindByIdAsync(TransactionId, TestContext.Current.CancellationToken)).Status);
    }

    [Fact]
    public async Task Status_WhenTheAmountEndsIn05_StaysPendingUntilTheSweepCompletesIt()
    {
        // Arrange
        var harness = new Harness(10.05m);

        await harness.Engine.BeginPaymentAsync(SessionId, new BeginPaymentOptions { ProviderKey = TestCheckoutPaymentProvider.ProviderKey }, TestContext.Current.CancellationToken);

        // The tenant's attempt store stamps this; the in-memory one used here does not.
        var attempt = Assert.Single(await harness.AttemptStore.GetBySessionAsync(SessionId, TestContext.Current.CancellationToken));
        attempt.CreatedUtc = harness.Clock.UtcNow;

        // Act
        var whilePolling = await harness.Engine.TryCompleteAsync(SessionId, TestContext.Current.CancellationToken);
        var sessionWhilePolling = (await harness.SessionStore.GetAsync(SessionId, TestContext.Current.CancellationToken)).Status;

        harness.Clock.UtcNow += TestCheckoutPaymentProvider.PendingSettlementDelay;

        var fromTheSweep = await harness.Engine.TryCompleteAsync(SessionId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(CheckoutCompletionStatus.Pending, whilePolling.Status);
        Assert.Equal(CheckoutSessionStatus.PaymentPending, sessionWhilePolling);
        Assert.Equal(CheckoutCompletionStatus.Completed, fromTheSweep.Status);
        Assert.Equal(TransactionStatus.Paid, (await harness.TransactionStore.FindByIdAsync(TransactionId, TestContext.Current.CancellationToken)).Status);
    }

    private sealed class Harness
    {
        public Harness(decimal balance)
        {
            Clock = new TestClock(_now);
            AttemptStore = new InMemoryPaymentAttemptStore();
            SessionStore = new InMemoryCheckoutSessionStore();
            TransactionStore = new FakeTransactionStore(new Transaction
            {
                ItemId = TransactionId,
                Title = "Registration fee",
                Currency = "USD",
                Amount = balance,
                TotalAmount = balance,
                Status = TransactionStatus.Outstanding,
                CreatedUtc = _now,
                UpdatedUtc = _now,
            });

            var provider = TestCheckoutPaymentProviderTests.CreateProvider(Clock);
            var providerResolver = new CheckoutPaymentProviderResolver([provider]);
            var manager = TransactionManagerFactory.Create(TransactionStore);

            var services = new ServiceCollection();
            services.AddSingleton<IPaymentAttemptStore>(AttemptStore);
            services.AddSingleton<IPaymentRefundHandler>(sp => new TransactionRefundHandler(
                manager,
                sp,
                Clock,
                new PassThroughStringLocalizer<TransactionRefundHandler>()));

            var serviceProvider = services.BuildServiceProvider();

            var settlementHandler = new TransactionSettlementCheckoutHandler(
                manager,
                AttemptStore,
                new TransactionSettlementService(
                    manager,
                    serviceProvider,
                    Clock,
                    NullLogger<TransactionSettlementService>.Instance,
                    new PassThroughStringLocalizer<TransactionSettlementService>()),
                providerResolver,
                serviceProvider,
                NullLogger<TransactionSettlementCheckoutHandler>.Instance,
                new PassThroughStringLocalizer<TransactionSettlementCheckoutHandler>());

            var distributedLock = new Mock<IDistributedLock>();
            distributedLock
                .Setup(l => l.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
                .ReturnsAsync((new NoopLocker(), true));

            RefundService = new DefaultCheckoutRefundService(
                SessionStore,
                AttemptStore,
                new InMemoryPaymentRefundStore(),
                new CheckoutPaymentRefundProviderResolver([provider], NullLogger<CheckoutPaymentRefundProviderResolver>.Instance),
                distributedLock.Object,
                [],
                Clock,
                serviceProvider,
                NullLogger<DefaultCheckoutRefundService>.Instance);

            Engine = new DefaultCheckoutEngine(
                SessionStore,
                AttemptStore,
                providerResolver,
                new CheckoutRecurringPaymentProviderResolver([provider], NullLogger<CheckoutRecurringPaymentProviderResolver>.Instance),
                new CheckoutReconciliationService(AttemptStore, providerResolver, NullLogger<CheckoutReconciliationService>.Instance),
                RefundService,
                [settlementHandler],
                distributedLock.Object,
                new Mock<ISession>().Object,
                Clock,
                NullLogger<DefaultCheckoutEngine>.Instance);

            SessionStore.Seed(CreateSession(balance));
        }

        public TestClock Clock { get; }

        public InMemoryPaymentAttemptStore AttemptStore { get; }

        public InMemoryCheckoutSessionStore SessionStore { get; }

        public FakeTransactionStore TransactionStore { get; }

        public DefaultCheckoutRefundService RefundService { get; }

        public DefaultCheckoutEngine Engine { get; }

        // The session a pay link starts for an outstanding transaction: the balance is its only line, and it must be
        // paid now.
        private static CheckoutSession CreateSession(decimal balance)
        {
            var session = new CheckoutSession
            {
                SessionId = SessionId,
                ReferenceType = TransactionsConstants.ReferenceTypes.Transaction,
                ReferenceId = TransactionId,
                Status = CheckoutSessionStatus.Pending,
                Currency = "USD",
                SettlesExistingBalance = true,
            };

            session.Steps.Add(new CheckoutFlowStep
            {
                Key = CheckoutConstants.PaymentStepKey,
                Order = int.MaxValue,
                CollectData = false,
            });

            session.Put(new CheckoutInvoice
            {
                Currency = "USD",
                InitialPaymentAmount = balance,
                DueNow = balance,
                GrandTotal = balance,
                LineItems = [new CheckoutLineItem { ItemId = TransactionId, Quantity = 1, UnitPrice = balance }],
            });

            return session;
        }
    }

    private sealed class NoopLocker : ILocker
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void Dispose()
        {
        }
    }
}
