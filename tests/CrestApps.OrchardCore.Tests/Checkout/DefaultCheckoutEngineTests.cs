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
using Xunit;
using ISession = YesSql.ISession;
using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// The engine is the only component allowed to move money, so these tests are about ordering and refusal:
/// a durable record exists before any provider is contacted, a checkout is never completed on anything but
/// the provider's own confirmation, and money already taken is returned when the purchase cannot complete.
/// </summary>
public sealed class DefaultCheckoutEngineTests
{
    private const string ProviderKey = "test-provider";
    private const string Currency = "USD";

    /// <summary>
    /// The single most important ordering guarantee: if the process dies between creating the attempt and
    /// hearing back from the gateway, the attempt is what lets the reconciliation sweep find the charge.
    /// Contacting the gateway first would make that charge invisible.
    /// </summary>
    [Fact]
    public async Task BeginPaymentAsync_PersistsTheAttemptBeforeCallingTheProvider()
    {
        var attemptStore = new InMemoryPaymentAttemptStore();
        var attemptsAtProviderCall = -1;

        var provider = new RecordingProvider(ProviderKey)
        {
            OnBegin = async () => attemptsAtProviderCall = (await attemptStore.GetBySessionAsync("session-1", TestContext.Current.CancellationToken)).Count(),
        };

        var (engine, sessionStore) = CreateEngine(attemptStore, provider);
        sessionStore.Seed(CreateSession(oneTimeAmount: 30m));

        var outcome = await engine.BeginPaymentAsync("session-1", new BeginPaymentOptions { ProviderKey = ProviderKey }, TestContext.Current.CancellationToken);

        Assert.True(outcome.Succeeded);
        Assert.Equal(1, attemptsAtProviderCall);
    }

    /// <summary>
    /// The provider's reference is the only way to find the remote resource again. Storing it immediately is
    /// what stops a later failure in the same checkout from orphaning a real charge.
    /// </summary>
    [Fact]
    public async Task BeginPaymentAsync_StoresTheProviderReferenceOnTheAttempt()
    {
        var attemptStore = new InMemoryPaymentAttemptStore();
        var (engine, sessionStore) = CreateEngine(attemptStore, new RecordingProvider(ProviderKey));
        sessionStore.Seed(CreateSession(oneTimeAmount: 30m));

        await engine.BeginPaymentAsync("session-1", new BeginPaymentOptions { ProviderKey = ProviderKey }, TestContext.Current.CancellationToken);

        var attempt = Assert.Single(await attemptStore.GetBySessionAsync("session-1", TestContext.Current.CancellationToken));

        Assert.Equal("provider-ref", attempt.ProviderReference);
        Assert.Equal(PaymentAttemptState.Pending, attempt.State);
        Assert.Equal(CheckoutObligations.OneTime, attempt.ObligationId);
    }

    /// <summary>
    /// A customer who refreshes the payment page or double-submits must not be charged twice, so beginning
    /// again resumes the existing attempts instead of creating a second set.
    /// </summary>
    [Fact]
    public async Task BeginPaymentAsync_ResumesExistingAttemptsInsteadOfCreatingDuplicates()
    {
        var attemptStore = new InMemoryPaymentAttemptStore();
        var provider = new RecordingProvider(ProviderKey);
        var (engine, sessionStore) = CreateEngine(attemptStore, provider);
        sessionStore.Seed(CreateSession(oneTimeAmount: 30m));

        await engine.BeginPaymentAsync("session-1", new BeginPaymentOptions { ProviderKey = ProviderKey }, TestContext.Current.CancellationToken);
        await engine.BeginPaymentAsync("session-1", new BeginPaymentOptions { ProviderKey = ProviderKey }, TestContext.Current.CancellationToken);

        Assert.Single(await attemptStore.GetBySessionAsync("session-1", TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// An invoice with an up-front amount and a recurring interval owes two distinct obligations, and each
    /// gets its own attempt so a partial failure is attributable and can be compensated.
    /// </summary>
    [Fact]
    public async Task BeginPaymentAsync_CreatesOneAttemptPerObligation()
    {
        var attemptStore = new InMemoryPaymentAttemptStore();
        var (engine, sessionStore) = CreateEngine(attemptStore, new RecordingProvider(ProviderKey));
        sessionStore.Seed(CreateSession(oneTimeAmount: 30m, recurringAmount: 10m));

        var outcome = await engine.BeginPaymentAsync("session-1", new BeginPaymentOptions { ProviderKey = ProviderKey }, TestContext.Current.CancellationToken);

        Assert.True(outcome.Succeeded);
        Assert.Equal(2, outcome.Steps.Count);

        var attempts = await attemptStore.GetBySessionAsync("session-1", TestContext.Current.CancellationToken);

        Assert.Equal(2, attempts.Count());
        Assert.Contains(attempts, attempt => attempt.ObligationId == CheckoutObligations.OneTime);
    }

    /// <summary>
    /// Discovering that a provider cannot express the invoice <em>after</em> charging the customer would mean
    /// refunding them for a configuration mistake, so the capability check happens before any money moves.
    /// </summary>
    [Fact]
    public async Task BeginPaymentAsync_RefusesAProviderThatCannotSetUpRecurringPayments()
    {
        var attemptStore = new InMemoryPaymentAttemptStore();

        var provider = new RecordingProvider(ProviderKey)
        {
            Capabilities = new PaymentProviderCapabilities
            {
                SupportsOneTimePayments = true,
                SupportsRecurringPayments = false,
            },
        };

        var (engine, sessionStore) = CreateEngine(attemptStore, provider);
        sessionStore.Seed(CreateSession(oneTimeAmount: 0m, recurringAmount: 10m));

        var outcome = await engine.BeginPaymentAsync("session-1", new BeginPaymentOptions { ProviderKey = ProviderKey }, TestContext.Current.CancellationToken);

        Assert.False(outcome.Succeeded);
        Assert.Empty(await attemptStore.GetBySessionAsync("session-1", TestContext.Current.CancellationToken));
        Assert.Equal(0, provider.BeginCallCount);
    }

    /// <summary>
    /// A recurring obligation must establish a billing agreement, not take one charge. Routing it down the
    /// one-time path would charge the customer once and then silently never bill them again.
    /// </summary>
    [Fact]
    public async Task BeginPaymentAsync_RoutesARecurringObligationToTheRecurringCapability()
    {
        var attemptStore = new InMemoryPaymentAttemptStore();
        var provider = new RecordingProvider(ProviderKey);
        var (engine, sessionStore) = CreateEngine(attemptStore, provider);
        sessionStore.Seed(CreateSession(oneTimeAmount: 0m, recurringAmount: 10m));

        var outcome = await engine.BeginPaymentAsync("session-1", new BeginPaymentOptions
        {
            ProviderKey = ProviderKey,
            ProviderData = new Dictionary<string, string> { ["paymentMethodId"] = "pm_123" },
        }, TestContext.Current.CancellationToken);

        Assert.True(outcome.Succeeded);
        Assert.Equal(1, provider.BeginRecurringCallCount);
        Assert.Equal(0, provider.BeginCallCount);

        var context = provider.LastRecurringContext;

        Assert.Equal(DurationType.Month, context.Interval.Type);
        Assert.Equal(1, context.Interval.Duration);
        Assert.Equal("recurring", Assert.Single(context.LineItems).ItemId);
        Assert.Equal("pm_123", context.ProviderData["paymentMethodId"]);
    }

    /// <summary>
    /// A provider whose declared capabilities promise recurring support but which ships no recurring
    /// implementation is a wiring mistake. Failing here is far better than taking a single charge for
    /// something the customer expects to renew.
    /// </summary>
    [Fact]
    public async Task BeginPaymentAsync_WhenNoRecurringImplementationIsRegistered_Fails()
    {
        var attemptStore = new InMemoryPaymentAttemptStore();
        var provider = new RecordingProvider(ProviderKey);
        var (engine, sessionStore) = CreateEngine(attemptStore, provider, supportsRecurring: false);
        sessionStore.Seed(CreateSession(oneTimeAmount: 0m, recurringAmount: 10m));

        var outcome = await engine.BeginPaymentAsync("session-1", new BeginPaymentOptions { ProviderKey = ProviderKey }, TestContext.Current.CancellationToken);

        Assert.False(outcome.Succeeded);
        Assert.Equal(0, provider.BeginCallCount);
    }

    [Fact]
    public async Task TryCompleteAsync_WhenProviderConfirms_CompletesAndRunsHandlers()
    {
        var attemptStore = new InMemoryPaymentAttemptStore();
        var handler = new RecordingHandler();
        var (engine, sessionStore) = CreateEngine(attemptStore, ConfirmingProvider(), handler);
        sessionStore.Seed(CreateSession(oneTimeAmount: 30m));

        await engine.BeginPaymentAsync("session-1", new BeginPaymentOptions { ProviderKey = ProviderKey }, TestContext.Current.CancellationToken);

        var result = await engine.TryCompleteAsync("session-1", TestContext.Current.CancellationToken);

        Assert.Equal(CheckoutCompletionStatus.Completed, result.Status);
        Assert.True(result.IsCompleted);
        Assert.Equal(1, handler.CompletingCount);
        Assert.Equal(1, handler.CompletedCount);

        var session = await sessionStore.GetAsync("session-1", TestContext.Current.CancellationToken);

        Assert.Equal(CheckoutSessionStatus.Completed, session.Status);
        Assert.NotNull(session.CompletedUtc);
    }

    /// <summary>
    /// Completion is driven from the browser, a webhook, and a background sweep at once. Fulfillment running
    /// twice would double-provision whatever was bought, so the transition guards it.
    /// </summary>
    [Fact]
    public async Task TryCompleteAsync_IsIdempotent_AndRunsFulfillmentOnlyOnce()
    {
        var attemptStore = new InMemoryPaymentAttemptStore();
        var handler = new RecordingHandler();
        var (engine, sessionStore) = CreateEngine(attemptStore, ConfirmingProvider(), handler);
        sessionStore.Seed(CreateSession(oneTimeAmount: 30m));

        await engine.BeginPaymentAsync("session-1", new BeginPaymentOptions { ProviderKey = ProviderKey }, TestContext.Current.CancellationToken);

        var first = await engine.TryCompleteAsync("session-1", TestContext.Current.CancellationToken);
        var second = await engine.TryCompleteAsync("session-1", TestContext.Current.CancellationToken);

        Assert.Equal(CheckoutCompletionStatus.Completed, first.Status);
        Assert.Equal(CheckoutCompletionStatus.AlreadyCompleted, second.Status);
        Assert.True(second.IsCompleted);
        Assert.Equal(1, handler.CompletedCount);
    }

    /// <summary>
    /// The browser polling, a provider webhook, and the reconciliation sweep can all try to finish the same
    /// checkout in the same instant. Exactly one of them may fulfill it: two would double-provision whatever
    /// the customer bought, and none would leave a paid checkout unfulfilled.
    /// </summary>
    [Fact]
    public async Task TryCompleteAsync_CalledConcurrently_FulfillsExactlyOnce()
    {
        var attemptStore = new InMemoryPaymentAttemptStore();
        var handler = new RecordingHandler();
        var (engine, sessionStore) = CreateEngine(attemptStore, ConfirmingProvider(), handler);
        sessionStore.Seed(CreateSession(oneTimeAmount: 30m));

        await engine.BeginPaymentAsync("session-1", new BeginPaymentOptions { ProviderKey = ProviderKey }, TestContext.Current.CancellationToken);

        var results = await Task.WhenAll(Enumerable
            .Range(0, 8)
            .Select(_ => engine.TryCompleteAsync("session-1", TestContext.Current.CancellationToken)));

        Assert.All(results, result => Assert.True(result.IsCompleted));
        Assert.Single(results, result => result.Status == CheckoutCompletionStatus.Completed);
        Assert.Equal(1, handler.CompletedCount);
    }

    /// <summary>
    /// Beginning the same payment twice must reuse the attempt rather than create a second one, because a
    /// second attempt is a second charge for the same obligation.
    /// </summary>
    [Fact]
    public async Task BeginPaymentAsync_CalledTwice_ReusesTheSameAttempt()
    {
        var attemptStore = new InMemoryPaymentAttemptStore();
        var provider = new RecordingProvider(ProviderKey);
        var (engine, sessionStore) = CreateEngine(attemptStore, provider);
        sessionStore.Seed(CreateSession(oneTimeAmount: 30m));

        await engine.BeginPaymentAsync("session-1", new BeginPaymentOptions { ProviderKey = ProviderKey }, TestContext.Current.CancellationToken);
        await engine.BeginPaymentAsync("session-1", new BeginPaymentOptions { ProviderKey = ProviderKey }, TestContext.Current.CancellationToken);

        var attempts = await attemptStore.GetBySessionAsync("session-1", TestContext.Current.CancellationToken);

        Assert.Single(attempts);
    }

    /// <summary>
    /// A provider that has not answered yet is not a failure. Reporting it as pending is what lets the client
    /// poll and the sweep retry, instead of a customer being told their successful payment failed.
    /// </summary>
    [Fact]
    public async Task TryCompleteAsync_WhenProviderHasNotConfirmed_ReportsPendingAndDoesNotFulfill()
    {
        var attemptStore = new InMemoryPaymentAttemptStore();
        var handler = new RecordingHandler();

        var provider = new RecordingProvider(ProviderKey)
        {
            Verification = _ => new PaymentVerificationResult { Status = PaymentStatus.Unknown },
        };

        var (engine, sessionStore) = CreateEngine(attemptStore, provider, handler);
        sessionStore.Seed(CreateSession(oneTimeAmount: 30m));

        await engine.BeginPaymentAsync("session-1", new BeginPaymentOptions { ProviderKey = ProviderKey }, TestContext.Current.CancellationToken);

        var result = await engine.TryCompleteAsync("session-1", TestContext.Current.CancellationToken);

        Assert.Equal(CheckoutCompletionStatus.Pending, result.Status);
        Assert.Contains(CheckoutObligations.OneTime, result.OutstandingObligationIds);
        Assert.Equal(0, handler.CompletedCount);

        var session = await sessionStore.GetAsync("session-1", TestContext.Current.CancellationToken);

        Assert.Equal(CheckoutSessionStatus.PaymentPending, session.Status);
    }

    [Fact]
    public async Task TryCompleteAsync_WhenProviderReportsFailure_FailsAndDoesNotFulfill()
    {
        var attemptStore = new InMemoryPaymentAttemptStore();
        var handler = new RecordingHandler();

        var provider = new RecordingProvider(ProviderKey)
        {
            Verification = _ => new PaymentVerificationResult { Status = PaymentStatus.Failed, TransactionId = "txn-failed" },
        };

        var (engine, sessionStore) = CreateEngine(attemptStore, provider, handler);
        sessionStore.Seed(CreateSession(oneTimeAmount: 30m));

        await engine.BeginPaymentAsync("session-1", new BeginPaymentOptions { ProviderKey = ProviderKey }, TestContext.Current.CancellationToken);

        var result = await engine.TryCompleteAsync("session-1", TestContext.Current.CancellationToken);

        Assert.Equal(CheckoutCompletionStatus.Failed, result.Status);
        Assert.Equal(0, handler.CompletedCount);
        Assert.True(handler.FailedCalled);

        var session = await sessionStore.GetAsync("session-1", TestContext.Current.CancellationToken);

        Assert.Equal(CheckoutSessionStatus.Failed, session.Status);
    }

    /// <summary>
    /// When one obligation fails the customer must not keep paying for the ones that succeeded. The refund
    /// goes through the durable refund service rather than straight to the gateway, so the gateway's own
    /// refund notification correlates to it instead of being quarantined for an operator.
    /// </summary>
    [Fact]
    public async Task TryCompleteAsync_WhenOneObligationFails_RefundsTheSettledOnes()
    {
        var attemptStore = new InMemoryPaymentAttemptStore();

        // The up-front amount settles; the recurring interval is declined.
        var provider = new RecordingProvider(ProviderKey)
        {
            Verification = context => context.Attempt.ObligationId == CheckoutObligations.OneTime
                ? new PaymentVerificationResult
                {
                    Status = PaymentStatus.Succeeded,
                    TransactionId = "txn-onetime",
                    ReportsAuthoritativeAmount = true,
                    Amount = context.Attempt.ExpectedAmount,
                    Currency = Currency,
                }
                : new PaymentVerificationResult { Status = PaymentStatus.Failed, TransactionId = "txn-recurring" },
        };

        var refundService = new RecordingRefundService();
        var (engine, sessionStore) = CreateEngine(attemptStore, provider, handler: null, refundService);
        sessionStore.Seed(CreateSession(oneTimeAmount: 30m, recurringAmount: 10m));

        await engine.BeginPaymentAsync("session-1", new BeginPaymentOptions { ProviderKey = ProviderKey }, TestContext.Current.CancellationToken);

        var result = await engine.TryCompleteAsync("session-1", TestContext.Current.CancellationToken);

        Assert.Equal(CheckoutCompletionStatus.Failed, result.Status);

        var refunded = Assert.Single(refundService.Requests);

        Assert.Equal("txn-onetime", refunded.OriginalTransactionId);
        Assert.Equal("session-1", refunded.SessionId);
    }

    /// <summary>
    /// A checkout whose data-collection steps are unfinished must send the customer back rather than fail, so
    /// the distinction is reported instead of being collapsed into a generic error.
    /// </summary>
    [Fact]
    public async Task TryCompleteAsync_WhenAStepIsIncomplete_ReportsBlocked()
    {
        var attemptStore = new InMemoryPaymentAttemptStore();
        var (engine, sessionStore) = CreateEngine(attemptStore, ConfirmingProvider());

        var session = CreateSession(oneTimeAmount: 30m);
        session.Steps.Insert(0, new CheckoutFlowStep { Key = "address", Order = 0, CollectData = true });
        sessionStore.Seed(session);

        var result = await engine.TryCompleteAsync("session-1", TestContext.Current.CancellationToken);

        Assert.Equal(CheckoutCompletionStatus.Blocked, result.Status);
        Assert.Equal("address", result.BlockingStepKey);
    }

    /// <summary>
    /// Nothing owed means nothing to settle, so a free checkout completes without ever contacting a provider.
    /// </summary>
    [Fact]
    public async Task TryCompleteAsync_WhenNothingIsOwed_CompletesWithoutAPayment()
    {
        var attemptStore = new InMemoryPaymentAttemptStore();
        var provider = new RecordingProvider(ProviderKey);
        var handler = new RecordingHandler();
        var (engine, sessionStore) = CreateEngine(attemptStore, provider, handler);
        sessionStore.Seed(CreateSession(oneTimeAmount: 0m));

        var result = await engine.TryCompleteAsync("session-1", TestContext.Current.CancellationToken);

        Assert.Equal(CheckoutCompletionStatus.Completed, result.Status);
        Assert.Equal(1, handler.CompletedCount);
        Assert.Equal(0, provider.BeginCallCount);
    }

    [Fact]
    public async Task TryCompleteAsync_WhenSessionIsMissing_ReportsNotFound()
    {
        var (engine, _) = CreateEngine(new InMemoryPaymentAttemptStore(), ConfirmingProvider());

        var result = await engine.TryCompleteAsync("missing", TestContext.Current.CancellationToken);

        Assert.Equal(CheckoutCompletionStatus.NotFound, result.Status);
    }

    /// <summary>
    /// Reversing a completed checkout is a refund, not a cancellation, so cancelling one must not quietly
    /// discard a purchase the customer already paid for and received.
    /// </summary>
    [Fact]
    public async Task CancelAsync_RefusesToCancelACompletedCheckout()
    {
        var attemptStore = new InMemoryPaymentAttemptStore();
        var (engine, sessionStore) = CreateEngine(attemptStore, ConfirmingProvider());
        sessionStore.Seed(CreateSession(oneTimeAmount: 30m));

        await engine.BeginPaymentAsync("session-1", new BeginPaymentOptions { ProviderKey = ProviderKey }, TestContext.Current.CancellationToken);
        await engine.TryCompleteAsync("session-1", TestContext.Current.CancellationToken);

        var result = await engine.CancelAsync("session-1", "changed my mind", TestContext.Current.CancellationToken);

        Assert.Equal(CheckoutCompletionStatus.AlreadyCompleted, result.Status);
        Assert.Equal(CheckoutSessionStatus.Completed, (await sessionStore.GetAsync("session-1", TestContext.Current.CancellationToken)).Status);
    }

    [Fact]
    public async Task CancelAsync_ReleasesTheRemoteResourcesOfPendingAttempts()
    {
        var attemptStore = new InMemoryPaymentAttemptStore();
        var provider = new RecordingProvider(ProviderKey);
        var (engine, sessionStore) = CreateEngine(attemptStore, provider);
        sessionStore.Seed(CreateSession(oneTimeAmount: 30m));

        await engine.BeginPaymentAsync("session-1", new BeginPaymentOptions { ProviderKey = ProviderKey }, TestContext.Current.CancellationToken);

        var result = await engine.CancelAsync("session-1", "abandoned", TestContext.Current.CancellationToken);

        Assert.Equal(CheckoutCompletionStatus.Canceled, result.Status);
        Assert.Equal(1, provider.CancelCallCount);

        var attempt = Assert.Single(await attemptStore.GetBySessionAsync("session-1", TestContext.Current.CancellationToken));

        Assert.Equal(PaymentAttemptState.Canceled, attempt.State);
    }

    /// <summary>
    /// The engine holds a per-session lock so two nodes cannot transition the same checkout at once. The
    /// loser reports pending rather than failing, because the checkout is about to succeed elsewhere.
    /// </summary>
    [Fact]
    public async Task TryCompleteAsync_WhenTheSessionLockIsHeldElsewhere_ReportsPending()
    {
        var attemptStore = new InMemoryPaymentAttemptStore();
        var (engine, sessionStore) = CreateEngine(attemptStore, ConfirmingProvider(), handler: null, refundService: null, lockGranted: false);
        sessionStore.Seed(CreateSession(oneTimeAmount: 30m));

        var result = await engine.TryCompleteAsync("session-1", TestContext.Current.CancellationToken);

        Assert.Equal(CheckoutCompletionStatus.Pending, result.Status);
    }

    private static RecordingProvider ConfirmingProvider()
        => new(ProviderKey)
        {
            Verification = context => new PaymentVerificationResult
            {
                Status = PaymentStatus.Succeeded,
                TransactionId = "txn-" + context.Attempt.ObligationId,
                ReportsAuthoritativeAmount = true,
                Amount = context.Attempt.ExpectedAmount,
                Currency = Currency,
            },
        };

    private static CheckoutSession CreateSession(decimal oneTimeAmount, decimal recurringAmount = 0m)
    {
        var session = new CheckoutSession
        {
            SessionId = "session-1",
            Status = CheckoutSessionStatus.Pending,
            Currency = Currency,
        };

        session.Steps.Add(new CheckoutFlowStep
        {
            Key = CheckoutConstants.PaymentStepKey,
            Order = int.MaxValue,
            CollectData = false,
        });

        var lineItems = new List<CheckoutLineItem>();

        if (oneTimeAmount > 0m)
        {
            lineItems.Add(new CheckoutLineItem { ItemId = "one-time", Quantity = 1, UnitPrice = oneTimeAmount });
        }

        if (recurringAmount > 0m)
        {
            lineItems.Add(new CheckoutLineItem
            {
                ItemId = "recurring",
                Quantity = 1,
                UnitPrice = recurringAmount,
                Plan = new RecurringPlan { DurationType = DurationType.Month, BillingDuration = 1 },
            });
        }

        session.Put(new CheckoutInvoice
        {
            Currency = Currency,
            InitialPaymentAmount = oneTimeAmount > 0m ? oneTimeAmount : null,
            FirstRecurringPaymentAmount = recurringAmount > 0m ? recurringAmount : null,
            DueNow = oneTimeAmount + recurringAmount,
            GrandTotal = oneTimeAmount + recurringAmount,
            LineItems = [.. lineItems],
        });

        return session;
    }

    private static (DefaultCheckoutEngine Engine, InMemoryCheckoutSessionStore SessionStore) CreateEngine(
        InMemoryPaymentAttemptStore attemptStore,
        RecordingProvider provider,
        RecordingHandler handler = null,
        RecordingRefundService refundService = null,
        bool lockGranted = true,
        bool supportsRecurring = true)
    {
        var sessionStore = new InMemoryCheckoutSessionStore();

        var providerResolver = new Mock<ICheckoutPaymentProviderResolver>();
        providerResolver.Setup(r => r.GetProvider(ProviderKey)).Returns(provider);

        var recurringResolver = new Mock<ICheckoutRecurringPaymentProviderResolver>();
        recurringResolver
            .Setup(r => r.GetProvider(ProviderKey))
            .Returns(supportsRecurring ? provider : null);

        var reconciliation = new CheckoutReconciliationService(
            attemptStore,
            providerResolver.Object,
            NullLogger<CheckoutReconciliationService>.Instance);

        var distributedLock = new Mock<IDistributedLock>();
        distributedLock
            .Setup(l => l.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync((new NoopLocker(), lockGranted));

        var engine = new DefaultCheckoutEngine(
            sessionStore,
            attemptStore,
            providerResolver.Object,
            recurringResolver.Object,
            reconciliation,
            refundService ?? new RecordingRefundService(),
            handler is null ? [] : [handler],
            distributedLock.Object,
            new Mock<ISession>().Object,
            new TestClock(DateTime.UtcNow),
            NullLogger<DefaultCheckoutEngine>.Instance);

        return (engine, sessionStore);
    }

    private sealed class RecordingProvider : ICheckoutPaymentProvider, ICheckoutRecurringPaymentProvider
    {
        public RecordingProvider(string key)
            => Key = key;

        public string Key { get; }

        public string DisplayName => Key;

        public PaymentProviderCapabilities Capabilities { get; set; } = new()
        {
            SupportsOneTimePayments = true,
            SupportsRecurringPayments = true,
            SupportsCombinedOneTimeAndRecurring = true,
        };

        public Func<VerifyPaymentContext, PaymentVerificationResult> Verification { get; set; }
            = _ => new PaymentVerificationResult { Status = PaymentStatus.Unknown };

        public Func<Task> OnBegin { get; set; }

        public int BeginCallCount { get; private set; }

        public int CancelCallCount { get; private set; }

        public async Task<PaymentBeginResult> BeginAsync(BeginPaymentContext context, CancellationToken cancellationToken = default)
        {
            BeginCallCount++;

            if (OnBegin is not null)
            {
                await OnBegin();
            }

            return PaymentBeginResult.Success("provider-ref");
        }

        public Task<PaymentVerificationResult> VerifyAsync(VerifyPaymentContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(Verification(context));

        public Task<PaymentCancelResult> CancelAsync(CancelPaymentContext context, CancellationToken cancellationToken = default)
        {
            CancelCallCount++;

            return Task.FromResult(PaymentCancelResult.Success());
        }

        public int BeginRecurringCallCount { get; private set; }

        public BeginRecurringPaymentContext LastRecurringContext { get; private set; }

        public Task<PaymentBeginResult> BeginRecurringAsync(BeginRecurringPaymentContext context, CancellationToken cancellationToken = default)
        {
            BeginRecurringCallCount++;
            LastRecurringContext = context;

            return Task.FromResult(PaymentBeginResult.Success("provider-ref"));
        }

        public Task<RecurringCancelResult> CancelRecurringAsync(CancelRecurringPaymentContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(RecurringCancelResult.Success());

        public Task<RecurringPauseResult> PauseRecurringAsync(PauseRecurringPaymentContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(RecurringPauseResult.Success());

        public Task<RecurringUpdateResult> UpdateRecurringAsync(UpdateRecurringPaymentContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(RecurringUpdateResult.Success());
    }

    private sealed class RecordingHandler : CheckoutHandlerBase
    {
        public int CompletingCount { get; private set; }

        public int CompletedCount { get; private set; }

        public bool FailedCalled { get; private set; }

        public override Task CompletingAsync(CheckoutFlowCompletingContext context)
        {
            CompletingCount++;

            return Task.CompletedTask;
        }

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

    private sealed class RecordingRefundService : ICheckoutRefundService
    {
        public List<RequestPaymentRefundContext> Requests { get; } = [];

        public Task<PaymentRefund> RequestRefundAsync(RequestPaymentRefundContext context, CancellationToken cancellationToken = default)
        {
            Requests.Add(context);

            return Task.FromResult(new PaymentRefund
            {
                ItemId = "refund-" + Requests.Count,
                SessionId = context.SessionId,
                OriginalTransactionId = context.OriginalTransactionId,
                Status = RefundStatus.Succeeded,
            });
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
