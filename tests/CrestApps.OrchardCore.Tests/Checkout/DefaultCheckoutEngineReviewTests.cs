using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Handlers;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Tests.Checkout.Fakes;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using ISession = YesSql.ISession;
using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// Pins the engine behaviours an independent review found wrong: what a trial and a discounted first cycle
/// expect to collect, what happens when nothing was ever begun, what happens when fulfilment fails after the
/// money was confirmed, and what an abandoned checkout may and may not do.
/// </summary>
public sealed class DefaultCheckoutEngineReviewTests
{
    private const string ProviderKey = "test-provider";
    private const string Currency = "USD";

    /// <summary>
    /// A trial collects nothing now. The attempt therefore expects nothing now, so a provider that honestly
    /// reports zero collected settles it. Expecting the cycle amount instead left every trial refused as a
    /// short payment and the checkout pending forever.
    /// </summary>
    [Fact]
    public async Task ATrialExpectsNothingNow_AndCompletesWhenTheProviderConfirmsZero()
    {
        var attemptStore = new InMemoryPaymentAttemptStore();
        var provider = new StubProvider(context => new PaymentVerificationResult
        {
            Status = PaymentStatus.Succeeded,
            TransactionId = "sub_1",
            ReportsAuthoritativeAmount = true,
            Amount = 0m,
            Currency = Currency,
        });

        var (engine, sessionStore, _) = CreateEngine(attemptStore, provider);
        sessionStore.Seed(CreateRecurringSession(cycleAmount: 25m, firstCycleAmount: 25m, trialDays: 14));

        var begun = await engine.BeginPaymentAsync("session-1", new BeginPaymentOptions { ProviderKey = ProviderKey }, TestContext.Current.CancellationToken);
        Assert.True(begun.Succeeded);

        var attempt = Assert.Single(await attemptStore.GetBySessionAsync("session-1", TestContext.Current.CancellationToken));
        Assert.Equal(0m, attempt.ExpectedAmount);

        // The gateway is still told the real price and the deferral.
        Assert.Equal(25m, provider.LastRecurringContext.CycleAmount);
        Assert.Equal(0m, provider.LastRecurringContext.FirstCycleAmount);
        Assert.Equal(14, provider.LastRecurringContext.TrialDays);

        var result = await engine.TryCompleteAsync("session-1", TestContext.Current.CancellationToken);

        Assert.Equal(CheckoutCompletionStatus.Completed, result.Status);
    }

    /// <summary>
    /// A first-cycle coupon reduces what is due now and nothing else. The attempt expects the discounted
    /// amount; the gateway is told the full cycle price. Deriving the price from the attempt billed the
    /// discount on every later cycle.
    /// </summary>
    [Fact]
    public async Task AFirstCycleDiscount_ReducesTheAttemptButNotTheRecurringPrice()
    {
        var attemptStore = new InMemoryPaymentAttemptStore();
        var provider = new StubProvider();
        var (engine, sessionStore, _) = CreateEngine(attemptStore, provider);
        sessionStore.Seed(CreateRecurringSession(cycleAmount: 20m, firstCycleAmount: 15m, trialDays: 0));

        await engine.BeginPaymentAsync("session-1", new BeginPaymentOptions { ProviderKey = ProviderKey }, TestContext.Current.CancellationToken);

        var attempt = Assert.Single(await attemptStore.GetBySessionAsync("session-1", TestContext.Current.CancellationToken));

        Assert.Equal(15m, attempt.ExpectedAmount);
        Assert.Equal(20m, provider.LastRecurringContext.CycleAmount);
        Assert.Equal(15m, provider.LastRecurringContext.FirstCycleAmount);
        Assert.Null(provider.LastRecurringContext.TrialDays);
    }

    /// <summary>
    /// A checkout submitted before any payment was begun is still the customer's to act on. Moving it to a
    /// state that only a provider can resolve sent them to a page that did not exist.
    /// </summary>
    [Fact]
    public async Task TryCompleteAsync_WithNoAttempts_LeavesTheSessionPending()
    {
        var attemptStore = new InMemoryPaymentAttemptStore();
        var (engine, sessionStore, _) = CreateEngine(attemptStore, new StubProvider());
        var session = CreateRecurringSession(cycleAmount: 20m, firstCycleAmount: 20m, trialDays: 0);
        sessionStore.Seed(session);

        var result = await engine.TryCompleteAsync("session-1", TestContext.Current.CancellationToken);

        Assert.Equal(CheckoutCompletionStatus.Pending, result.Status);
        Assert.Equal(CheckoutSessionStatus.Pending, session.Status);
        Assert.Equal(0, sessionStore.SaveCount);
    }

    /// <summary>
    /// When a completing handler throws, the confirmed payments were already committed and the checkout is
    /// left for the sweep to retry. It must not be marked failed: that write would be discarded with the
    /// rest of the rolled-back transaction, and the customer has already paid.
    /// </summary>
    [Fact]
    public async Task TryCompleteAsync_WhenFulfilmentThrows_KeepsTheConfirmedPaymentAndReportsPending()
    {
        var attemptStore = new InMemoryPaymentAttemptStore();
        var provider = new StubProvider(context => new PaymentVerificationResult
        {
            Status = PaymentStatus.Succeeded,
            TransactionId = "txn-1",
            ReportsAuthoritativeAmount = true,
            Amount = context.Attempt.ExpectedAmount,
            Currency = Currency,
        });

        var handler = new ThrowingHandler();
        var (engine, sessionStore, dbSession) = CreateEngine(attemptStore, provider, handler);
        var session = CreateRecurringSession(cycleAmount: 20m, firstCycleAmount: 20m, trialDays: 0);
        sessionStore.Seed(session);

        await engine.BeginPaymentAsync("session-1", new BeginPaymentOptions { ProviderKey = ProviderKey }, TestContext.Current.CancellationToken);

        var result = await engine.TryCompleteAsync("session-1", TestContext.Current.CancellationToken);

        Assert.Equal(CheckoutCompletionStatus.Pending, result.Status);
        Assert.Equal(CheckoutSessionStatus.PaymentPending, session.Status);
        Assert.Equal(0, handler.CompletedCount);
        Assert.False(handler.FailedCalled);

        // The confirmation was committed before fulfilment ran, so the retry does not start from scratch.
        dbSession.Verify(s => s.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeast(2));
        dbSession.Verify(s => s.CancelAsync(), Times.Once);

        var attempt = Assert.Single(await attemptStore.GetBySessionAsync("session-1", TestContext.Current.CancellationToken));
        Assert.Equal(PaymentAttemptState.Succeeded, attempt.State);
    }

    /// <summary>
    /// Expiring releases what a provider was holding for a checkout nobody finished, and cancels its pending
    /// attempts so the remote resources are not orphaned.
    /// </summary>
    [Fact]
    public async Task ExpireAsync_ClosesAnUnpaidCheckoutAndCancelsItsAttempts()
    {
        var attemptStore = new InMemoryPaymentAttemptStore();
        var provider = new StubProvider();
        var (engine, sessionStore, _) = CreateEngine(attemptStore, provider);
        var session = CreateRecurringSession(cycleAmount: 20m, firstCycleAmount: 20m, trialDays: 0);
        sessionStore.Seed(session);

        await engine.BeginPaymentAsync("session-1", new BeginPaymentOptions { ProviderKey = ProviderKey }, TestContext.Current.CancellationToken);

        var result = await engine.ExpireAsync("session-1", TestContext.Current.CancellationToken);

        Assert.Equal(CheckoutCompletionStatus.Canceled, result.Status);
        Assert.Equal(CheckoutSessionStatus.Expired, session.Status);
        Assert.Equal(1, provider.CancelCallCount);
        Assert.All(await attemptStore.GetBySessionAsync("session-1", TestContext.Current.CancellationToken), attempt => Assert.Equal(PaymentAttemptState.Canceled, attempt.State));
    }

    /// <summary>
    /// Money that was taken is never expired away. A checkout with a confirmed attempt is an unfulfilled
    /// purchase for the sweep to finish, and the expiry must leave it exactly as it found it.
    /// </summary>
    [Fact]
    public async Task ExpireAsync_RefusesACheckoutThatCollectedMoney()
    {
        var attemptStore = new InMemoryPaymentAttemptStore(new PaymentAttempt
        {
            ItemId = "attempt-1",
            SessionId = "session-1",
            ProviderKey = ProviderKey,
            ObligationId = CheckoutObligations.OneTime,
            State = PaymentAttemptState.Succeeded,
            TransactionId = "txn-1",
            Currency = Currency,
        });

        var provider = new StubProvider();
        var (engine, sessionStore, _) = CreateEngine(attemptStore, provider);
        var session = CreateRecurringSession(cycleAmount: 20m, firstCycleAmount: 20m, trialDays: 0);
        session.Status = CheckoutSessionStatus.PaymentPending;
        sessionStore.Seed(session);

        var result = await engine.ExpireAsync("session-1", TestContext.Current.CancellationToken);

        Assert.Equal(CheckoutCompletionStatus.Pending, result.Status);
        Assert.Equal(CheckoutSessionStatus.PaymentPending, session.Status);
        Assert.Equal(0, provider.CancelCallCount);
        Assert.Equal(0, sessionStore.SaveCount);
    }

    private static CheckoutSession CreateRecurringSession(decimal cycleAmount, decimal firstCycleAmount, int trialDays)
    {
        var session = new CheckoutSession
        {
            SessionId = "session-1",
            Status = CheckoutSessionStatus.Pending,
            Currency = Currency,
        };

        session.Steps.Add(new CheckoutFlowStep { Key = CheckoutConstants.PaymentStepKey, Order = int.MaxValue });

        var deferred = trialDays > 0;

        session.Put(new CheckoutInvoice
        {
            Currency = Currency,
            FirstRecurringPaymentAmount = deferred ? null : firstCycleAmount,
            DueNow = deferred ? 0m : firstCycleAmount,
            GrandTotal = deferred ? 0m : firstCycleAmount,
            LineItems =
            [
                new CheckoutLineItem
                {
                    ItemId = "plan",
                    Quantity = 1,
                    UnitPrice = cycleAmount,
                    Plan = new RecurringPlan
                    {
                        DurationType = DurationType.Month,
                        BillingDuration = 1,
                        TrialDays = deferred ? trialDays : null,
                    },
                },
            ],
        });

        return session;
    }

    private static (DefaultCheckoutEngine Engine, InMemoryCheckoutSessionStore SessionStore, Mock<ISession> DbSession) CreateEngine(
        InMemoryPaymentAttemptStore attemptStore,
        StubProvider provider,
        ICheckoutHandler handler = null)
    {
        var sessionStore = new InMemoryCheckoutSessionStore();

        var providerResolver = new Mock<ICheckoutPaymentProviderResolver>();
        providerResolver.Setup(r => r.GetProvider(ProviderKey)).Returns(provider);

        var recurringResolver = new Mock<ICheckoutRecurringPaymentProviderResolver>();
        recurringResolver.Setup(r => r.GetProvider(ProviderKey)).Returns(provider);

        var reconciliation = new CheckoutReconciliationService(attemptStore, providerResolver.Object, NullLogger<CheckoutReconciliationService>.Instance);

        var distributedLock = new Mock<IDistributedLock>();
        distributedLock
            .Setup(l => l.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync((new NoopLocker(), true));

        var dbSession = new Mock<ISession>();

        var engine = new DefaultCheckoutEngine(
            sessionStore,
            attemptStore,
            providerResolver.Object,
            recurringResolver.Object,
            reconciliation,
            Mock.Of<ICheckoutRefundService>(),
            handler is null ? [] : [handler],
            distributedLock.Object,
            dbSession.Object,
            new TestClock(DateTime.UtcNow),
            NullLogger<DefaultCheckoutEngine>.Instance);

        return (engine, sessionStore, dbSession);
    }

    private sealed class StubProvider : ICheckoutPaymentProvider, ICheckoutRecurringPaymentProvider
    {
        private readonly Func<VerifyPaymentContext, PaymentVerificationResult> _verification;

        public StubProvider(Func<VerifyPaymentContext, PaymentVerificationResult> verification = null)
            => _verification = verification ?? (_ => new PaymentVerificationResult { Status = PaymentStatus.Unknown });

        public string Key => ProviderKey;

        public string DisplayName => ProviderKey;

        public PaymentProviderCapabilities Capabilities { get; } = new()
        {
            SupportsOneTimePayments = true,
            SupportsRecurringPayments = true,
            SupportsCombinedOneTimeAndRecurring = true,
        };

        public BeginRecurringPaymentContext LastRecurringContext { get; private set; }

        public int CancelCallCount { get; private set; }

        public Task<PaymentBeginResult> BeginAsync(BeginPaymentContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(PaymentBeginResult.Success("provider-ref"));

        public Task<PaymentVerificationResult> VerifyAsync(VerifyPaymentContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(_verification(context));

        public Task<PaymentCancelResult> CancelAsync(CancelPaymentContext context, CancellationToken cancellationToken = default)
        {
            CancelCallCount++;

            return Task.FromResult(PaymentCancelResult.Success());
        }

        public Task<PaymentBeginResult> BeginRecurringAsync(BeginRecurringPaymentContext context, CancellationToken cancellationToken = default)
        {
            LastRecurringContext = context;

            return Task.FromResult(PaymentBeginResult.Success("sub_1"));
        }

        public Task<RecurringCancelResult> CancelRecurringAsync(CancelRecurringPaymentContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(RecurringCancelResult.Success());

        public Task<RecurringPauseResult> PauseRecurringAsync(PauseRecurringPaymentContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(RecurringPauseResult.Success());

        public Task<RecurringUpdateResult> UpdateRecurringAsync(UpdateRecurringPaymentContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(RecurringUpdateResult.Success());
    }

    private sealed class ThrowingHandler : CheckoutHandlerBase
    {
        public int CompletedCount { get; private set; }

        public bool FailedCalled { get; private set; }

        public override Task CompletingAsync(CheckoutFlowCompletingContext context)
            => throw new InvalidOperationException("Fulfilment is broken.");

        public override Task CompletedAsync(CheckoutFlowCompletedContext context)
        {
            CompletedCount++;

            return Task.CompletedTask;
        }

        public override Task FailedAsync(CheckoutFlowFailedContext context)
        {
            FailedCalled = true;

            return Task.CompletedTask;
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
