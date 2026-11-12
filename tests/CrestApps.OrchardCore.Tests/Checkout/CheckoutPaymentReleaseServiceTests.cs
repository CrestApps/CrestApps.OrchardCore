using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Tests.Checkout.Fakes;
using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using Xunit;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// SEC-C1 step 2: an edit to a checkout (shipping, a code, a balance) supersedes a payment begun for the old total, so
/// the stale client secret is dead and the buyer pays the new total on the next begin. A payment the provider says is
/// already taken, or won't cancel, is left alone and the edit is refused instead.
/// </summary>
public sealed class CheckoutPaymentReleaseServiceTests
{
    private const string ProviderKey = "test-provider";

    [Fact]
    public async Task ReleaseAsync_WithALivePayment_CancelsItAtTheProviderAndSetsItAside()
    {
        var (service, attempts, provider) = Create(PaymentStatus.Unknown, cancelSucceeds: true);

        var result = await service.ReleaseAsync("session-1", TestContext.Current.CancellationToken);

        Assert.Equal(CheckoutPaymentReleaseResult.Released, result);
        Assert.Equal(1, provider.CancelCalls);

        var attempt = Assert.Single(await attempts.GetBySessionAsync("session-1", TestContext.Current.CancellationToken));
        Assert.Equal(PaymentAttemptState.Canceled, attempt.State);
        Assert.True(attempt.IsSuperseded);
    }

    [Fact]
    public async Task ReleaseAsync_WhenThePaymentWasAlreadyTaken_LeavesItAndRefusesTheEdit()
    {
        var (service, attempts, provider) = Create(PaymentStatus.Succeeded, cancelSucceeds: true);

        var result = await service.ReleaseAsync("session-1", TestContext.Current.CancellationToken);

        Assert.Equal(CheckoutPaymentReleaseResult.PaymentInProgress, result);
        Assert.Equal(0, provider.CancelCalls);
        Assert.Equal(PaymentAttemptState.Pending, Assert.Single(await attempts.GetBySessionAsync("session-1", TestContext.Current.CancellationToken)).State);
    }

    [Fact]
    public async Task ReleaseAsync_WhenTheProviderWontCancel_RefusesTheEdit()
    {
        var (service, attempts, _) = Create(PaymentStatus.Unknown, cancelSucceeds: false);

        var result = await service.ReleaseAsync("session-1", TestContext.Current.CancellationToken);

        Assert.Equal(CheckoutPaymentReleaseResult.PaymentInProgress, result);
        Assert.False(Assert.Single(await attempts.GetBySessionAsync("session-1", TestContext.Current.CancellationToken)).IsSuperseded);
    }

    [Fact]
    public async Task ReleaseAsync_WithNoLivePayment_DoesNothing()
    {
        var (service, _, provider) = Create(attemptState: null);

        Assert.Equal(CheckoutPaymentReleaseResult.Released, await service.ReleaseAsync("session-1", TestContext.Current.CancellationToken));
        Assert.Equal(0, provider.CancelCalls);
    }

    private static (CheckoutPaymentReleaseService Service, InMemoryPaymentAttemptStore Attempts, CancelRecordingProvider Provider) Create(
        PaymentStatus verification = PaymentStatus.Unknown,
        bool cancelSucceeds = true,
        PaymentAttemptState? attemptState = PaymentAttemptState.Pending)
    {
        var attempts = new InMemoryPaymentAttemptStore();

        if (attemptState is { } state)
        {
            attempts.CreateAsync(new PaymentAttempt
            {
                ItemId = "attempt-1",
                SessionId = "session-1",
                ProviderKey = ProviderKey,
                ObligationId = CheckoutObligations.OneTime,
                ExpectedAmount = 30m,
                Currency = "USD",
                State = state,
                ProviderReference = "pi_1",
            }).AsTask().GetAwaiter().GetResult();
        }

        var sessionStore = new InMemoryCheckoutSessionStore();
        sessionStore.Seed(new CheckoutSession { SessionId = "session-1", Status = CheckoutSessionStatus.AwaitingProvider, Currency = "USD" });

        var provider = new CancelRecordingProvider(verification, cancelSucceeds);
        var resolver = new Mock<ICheckoutPaymentProviderResolver>();
        resolver.Setup(r => r.GetProvider(ProviderKey)).Returns(provider);

        var distributedLock = new Mock<IDistributedLock>();
        distributedLock
            .Setup(l => l.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync((Mock.Of<ILocker>(), true));

        var service = new CheckoutPaymentReleaseService(
            sessionStore,
            attempts,
            resolver.Object,
            distributedLock.Object,
            new Mock<ISession>().Object,
            NullLogger<CheckoutPaymentReleaseService>.Instance);

        return (service, attempts, provider);
    }

    private sealed class CancelRecordingProvider : ICheckoutPaymentProvider
    {
        private readonly PaymentStatus _verification;
        private readonly bool _cancelSucceeds;

        public CancelRecordingProvider(PaymentStatus verification, bool cancelSucceeds)
        {
            _verification = verification;
            _cancelSucceeds = cancelSucceeds;
        }

        public int CancelCalls { get; private set; }

        public string Key => ProviderKey;

        public string DisplayName => ProviderKey;

        public PaymentProviderCapabilities Capabilities { get; } = new() { SupportsOneTimePayments = true };

        public Task<PaymentBeginResult> BeginAsync(BeginPaymentContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(PaymentBeginResult.Success("ref"));

        public Task<PaymentVerificationResult> VerifyAsync(VerifyPaymentContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(new PaymentVerificationResult { Status = _verification, TransactionId = _verification == PaymentStatus.Succeeded ? "txn" : null });

        public Task<PaymentCancelResult> CancelAsync(CancelPaymentContext context, CancellationToken cancellationToken = default)
        {
            CancelCalls++;

            return Task.FromResult(_cancelSucceeds ? PaymentCancelResult.Success() : PaymentCancelResult.Failure("processing"));
        }
    }
}
