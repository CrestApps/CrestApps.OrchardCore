using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Tests.Checkout;
using CrestApps.OrchardCore.Tests.Checkout.Fakes;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Transactions;

/// <summary>
/// A pay link opened in two tabs, or posted twice, starts two checkouts for the same transaction. They are driven here
/// through the real checkout engine, its payment gates and the test payment provider: only one of them may take the
/// money, while a transaction that takes part payments can still be paid in parts, one after the other.
/// </summary>
public sealed class TransactionPayLinkDoublePaymentTests
{
    private const string TransactionId = "transaction-1";

    private static readonly DateTime _now = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task TwoCheckoutsOfOnePayLink_PaidOneAfterTheOther_OnlyTheFirstCaptures()
    {
        // Arrange: both tabs were opened while 108 was owed.
        var harness = new Harness(108m);

        harness.SeedSession("tab-1", 108m);
        harness.SeedSession("tab-2", 108m);

        // Act: the second tab is paid before the first one's payment was applied to the transaction.
        var first = await harness.BeginAsync("tab-1");
        var second = await harness.BeginAsync("tab-2");

        await harness.Engine.TryCompleteAsync("tab-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(first.Succeeded);
        Assert.False(second.Succeeded);
        Assert.False(second.Declined);
        Assert.Empty(await harness.AttemptStore.GetBySessionAsync("tab-2", TestContext.Current.CancellationToken));
        Assert.Equal(1, await harness.CountCapturedAsync());

        var transaction = await harness.TransactionStore.FindByIdAsync(TransactionId, TestContext.Current.CancellationToken);

        Assert.Equal(TransactionStatus.Paid, transaction.Status);
        Assert.Equal(108m, transaction.AmountPaid);
    }

    [Fact]
    public async Task TwoCheckoutsOfOnePayLink_PaidAtTheSameMoment_OnlyOneCaptures()
    {
        // Arrange
        var harness = new Harness(108m);

        harness.SeedSession("tab-1", 108m);
        harness.SeedSession("tab-2", 108m);

        // Act
        var outcomes = await Task.WhenAll(
            Task.Run(() => harness.BeginAsync("tab-1"), TestContext.Current.CancellationToken),
            Task.Run(() => harness.BeginAsync("tab-2"), TestContext.Current.CancellationToken));

        // Assert
        Assert.Single(outcomes, outcome => outcome.Succeeded);
        Assert.Equal(1, await harness.CountCapturedAsync());
    }

    [Fact]
    public async Task ASecondCheckout_AfterTheFirstWasDeclined_CanPay()
    {
        // Arrange: an amount ending in .02 is declined by the test provider.
        var harness = new Harness(10.02m);

        harness.SeedSession("tab-1", 10.02m);
        harness.SeedSession("tab-2", 10.02m);

        var first = await harness.BeginAsync("tab-1");

        // Act
        var second = await harness.BeginAsync("tab-2");

        // Assert: the provider was asked (and declined again), so the first tab's declined payment held nothing.
        Assert.True(first.Declined);
        Assert.True(second.Declined);
        Assert.Single(await harness.AttemptStore.GetBySessionAsync("tab-2", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ASecondCheckout_WhoseFirstNeverReachedTheProvider_CanPayOnceTheHoldLapses()
    {
        // Arrange: the first tab's payment was let through the gate, but never reached the provider.
        var harness = new Harness(108m);

        harness.SeedSession("tab-1", 108m);
        harness.SeedSession("tab-2", 108m);

        var gate = harness.CreateGate();
        var context = new CheckoutPaymentBeginningContext(new CheckoutFlow(await harness.SessionStore.GetAsync("tab-1", TestContext.Current.CancellationToken)), TestCheckoutPaymentProvider.ProviderKey);

        await gate.BeginningAsync(context, TestContext.Current.CancellationToken);

        var meanwhile = await harness.BeginAsync("tab-2");

        harness.Clock.UtcNow += TimeSpan.FromMinutes(3);

        // Act
        var later = await harness.BeginAsync("tab-2");

        // Assert
        Assert.False(context.IsRefused);
        Assert.False(meanwhile.Succeeded);
        Assert.True(later.Succeeded);
        Assert.Equal(1, await harness.CountCapturedAsync());
    }

    [Fact]
    public async Task TwoPartPayments_OneAfterTheOther_AreBothCaptured()
    {
        // Arrange: the transaction takes part payments, and each tab asked to pay 40 of the 108 owed.
        var harness = new Harness(108m, allowPartialPayment: true);

        harness.SeedSession("tab-1", 40m, requested: 40m);
        harness.SeedSession("tab-2", 40m, requested: 40m);

        // Act
        var first = await harness.BeginAsync("tab-1");
        var whileTheFirstIsBeingPaid = await harness.BeginAsync("tab-2");

        await harness.Engine.TryCompleteAsync("tab-1", TestContext.Current.CancellationToken);

        var afterTheFirstWasApplied = await harness.BeginAsync("tab-2");

        await harness.Engine.TryCompleteAsync("tab-2", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(first.Succeeded);
        Assert.False(whileTheFirstIsBeingPaid.Succeeded);
        Assert.True(afterTheFirstWasApplied.Succeeded);
        Assert.Equal(2, await harness.CountCapturedAsync());

        var transaction = await harness.TransactionStore.FindByIdAsync(TransactionId, TestContext.Current.CancellationToken);

        Assert.Equal(80m, transaction.AmountPaid);
        Assert.Equal(28m, transaction.OutstandingAmount);
    }

    private sealed class Harness
    {
        private readonly IServiceProvider _gateServices;

        public Harness(decimal balance, bool allowPartialPayment = false)
        {
            Clock = new TestClock(_now);
            AttemptStore = new InMemoryPaymentAttemptStore();
            SessionStore = new InMemoryCheckoutSessionStore();
            TransactionStore = new FakeTransactionStore(new Transaction
            {
                ItemId = TransactionId,
                Title = "Invoice",
                Currency = "USD",
                Amount = balance,
                TotalAmount = balance,
                AllowPartialPayment = allowPartialPayment,
                MinimumPayment = allowPartialPayment ? 20m : null,
                Status = TransactionStatus.Outstanding,
                CreatedUtc = _now,
                UpdatedUtc = _now,
            });

            var provider = TestCheckoutPaymentProviderTests.CreateProvider(Clock);
            var providerResolver = new CheckoutPaymentProviderResolver([provider]);
            var manager = TransactionManagerFactory.Create(TransactionStore);
            var distributedLock = new LocalLock(NullLogger<LocalLock>.Instance);

            var services = new ServiceCollection();
            services.AddSingleton<IPaymentAttemptStore>(AttemptStore);
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

            // The gate is built the way the container builds it, from the services a tenant has.
            _gateServices = new ServiceCollection()
                .AddSingleton<ITransactionManager>(manager)
                .AddSingleton<IPaymentAttemptStore>(AttemptStore)
                .AddSingleton<ICheckoutSessionStore>(SessionStore)
                .AddSingleton<IDistributedLock>(distributedLock)
                .AddSingleton(Mock.Of<ISession>())
                .AddSingleton<IClock>(Clock)
                .AddSingleton<IStringLocalizer<TransactionSettlementPaymentGate>>(new PassThroughStringLocalizer<TransactionSettlementPaymentGate>())
                .BuildServiceProvider();

            Engine = new DefaultCheckoutEngine(
                SessionStore,
                AttemptStore,
                providerResolver,
                new CheckoutRecurringPaymentProviderResolver([provider], NullLogger<CheckoutRecurringPaymentProviderResolver>.Instance),
                new CheckoutReconciliationService(AttemptStore, providerResolver, NullLogger<CheckoutReconciliationService>.Instance),
                Mock.Of<ICheckoutRefundService>(),
                [settlementHandler],
                distributedLock,
                Mock.Of<ISession>(),
                Clock,
                NullLogger<DefaultCheckoutEngine>.Instance,
                paymentGates: [CreateGate()]);
        }

        public TestClock Clock { get; }

        public InMemoryPaymentAttemptStore AttemptStore { get; }

        public InMemoryCheckoutSessionStore SessionStore { get; }

        public FakeTransactionStore TransactionStore { get; }

        public DefaultCheckoutEngine Engine { get; }

        public TransactionSettlementPaymentGate CreateGate()
            => ActivatorUtilities.CreateInstance<TransactionSettlementPaymentGate>(_gateServices);

        public Task<PaymentBeginOutcome> BeginAsync(string sessionId)
            => Engine.BeginPaymentAsync(sessionId, new BeginPaymentOptions { ProviderKey = TestCheckoutPaymentProvider.ProviderKey }, TestContext.Current.CancellationToken);

        // Attempts the provider took money for, across every checkout of the transaction.
        public async Task<int> CountCapturedAsync()
            => (await AttemptStore.GetByReferenceAsync(TransactionsConstants.ReferenceTypes.Transaction, TransactionId, TestContext.Current.CancellationToken))
                .Count(attempt => attempt.State is PaymentAttemptState.Succeeded or PaymentAttemptState.Pending);

        // The checkout the pay link starts: the balance (or the part asked for) is the settlement step's only line.
        public void SeedSession(string sessionId, decimal amount, decimal? requested = null)
        {
            var session = new CheckoutSession
            {
                SessionId = sessionId,
                ReferenceType = TransactionsConstants.ReferenceTypes.Transaction,
                ReferenceId = TransactionId,
                Status = CheckoutSessionStatus.Pending,
                Currency = "USD",
                SettlesExistingBalance = true,
            };

            session.Steps.Add(new CheckoutFlowStep
            {
                Key = TransactionsConstants.SettlementStepKey,
                Order = 0,
                CollectData = false,
                BillingItems = [new BillingItem { ItemId = TransactionId, Amount = amount, ExcludeFromTax = true }],
            });

            session.Steps.Add(new CheckoutFlowStep
            {
                Key = CheckoutConstants.PaymentStepKey,
                Order = int.MaxValue,
                CollectData = false,
            });

            if (requested is decimal asked)
            {
                session.Put(new CheckoutRequestedAmount { Amount = asked });
            }

            session.Put(new CheckoutInvoice
            {
                Currency = "USD",
                InitialPaymentAmount = amount,
                DueNow = amount,
                GrandTotal = amount,
                LineItems = [new CheckoutLineItem { ItemId = TransactionId, Quantity = 1, UnitPrice = amount }],
            });

            SessionStore.Seed(session);
        }
    }
}
