using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Tests.Checkout.Fakes;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using Xunit;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// A charge made without the payer present (a saved card on a payment plan) is refused at the gateway or not at
/// all: there is no browser to retry in. These tests pin how the engine records such a refusal and how it starts a
/// checkout on someone else's behalf.
/// </summary>
public sealed class DefaultCheckoutEngineSavedPaymentTests
{
    private const string ProviderKey = "card";

    [Fact]
    public async Task BeginPaymentAsync_WhenTheGatewayDeclines_FailsTheAttemptAndReportsTheReason()
    {
        // Arrange
        var attempts = new InMemoryPaymentAttemptStore();
        var provider = new ScriptedProvider { Result = PaymentBeginResult.Decline("Your card has insufficient funds.", "pi_declined") };
        var (engine, sessions) = CreateEngine(attempts, provider);
        sessions.Seed(CreateSession());

        // Act
        var outcome = await engine.BeginPaymentAsync("session-1", new BeginPaymentOptions { ProviderKey = ProviderKey }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(outcome.Succeeded);
        Assert.True(outcome.Declined);
        Assert.Equal("Your card has insufficient funds.", outcome.ProviderErrorMessage);

        var attempt = Assert.Single(await attempts.GetBySessionAsync("session-1", TestContext.Current.CancellationToken));

        Assert.Equal(PaymentAttemptState.Failed, attempt.State);
        Assert.Equal("Your card has insufficient funds.", attempt.FailureReason);
        Assert.Equal("pi_declined", attempt.ProviderReference);
    }

    [Fact]
    public async Task BeginPaymentAsync_WhenTheProviderCannotStart_LeavesTheAttemptToBeResumed()
    {
        // Arrange
        var attempts = new InMemoryPaymentAttemptStore();
        var provider = new ScriptedProvider { Result = PaymentBeginResult.Failure("The gateway is unreachable.") };
        var (engine, sessions) = CreateEngine(attempts, provider);
        sessions.Seed(CreateSession());

        // Act
        var outcome = await engine.BeginPaymentAsync("session-1", new BeginPaymentOptions { ProviderKey = ProviderKey }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(outcome.Succeeded);
        Assert.False(outcome.Declined);
        Assert.Equal("The gateway is unreachable.", outcome.ProviderErrorMessage);

        var attempt = Assert.Single(await attempts.GetBySessionAsync("session-1", TestContext.Current.CancellationToken));

        Assert.Equal(PaymentAttemptState.Created, attempt.State);
    }

    [Fact]
    public async Task BeginPaymentAsync_AfterADecline_GivesTheNewAttemptItsOwnIdempotencyKey()
    {
        // Arrange
        var attempts = new InMemoryPaymentAttemptStore();
        var provider = new ScriptedProvider { Result = PaymentBeginResult.Decline("Declined.") };
        var (engine, sessions) = CreateEngine(attempts, provider);
        sessions.Seed(CreateSession());

        await engine.BeginPaymentAsync("session-1", new BeginPaymentOptions { ProviderKey = ProviderKey }, TestContext.Current.CancellationToken);

        provider.Result = PaymentBeginResult.Success("pi_second");

        // Act
        var outcome = await engine.BeginPaymentAsync("session-1", new BeginPaymentOptions { ProviderKey = ProviderKey }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(outcome.Succeeded);

        var all = (await attempts.GetBySessionAsync("session-1", TestContext.Current.CancellationToken)).ToArray();

        Assert.Equal(2, all.Length);
        Assert.Equal(2, all.Select(attempt => attempt.IdempotencyKey).Distinct(StringComparer.Ordinal).Count());
        Assert.EndsWith("_retry1", all.Single(attempt => attempt.State == PaymentAttemptState.Pending).IdempotencyKey, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartAsync_WithAnOwner_BelongsToThatOwnerAndNotTheCaller()
    {
        // Arrange
        var (engine, sessions) = CreateEngine(new InMemoryPaymentAttemptStore(), new ScriptedProvider(), sessionStore: new OwnerStampingSessionStore("admin-1"));

        // Act
        var session = await engine.StartAsync(new StartCheckoutRequest
        {
            ReferenceType = "Transaction",
            ReferenceId = "tx-1",
            OwnerId = "customer-1",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("customer-1", session.OwnerId);
        Assert.Null(session.GuestTokenHash);
        Assert.Null(session.IPAddress);
    }

    [Fact]
    public async Task StartAsync_WithoutAnOwner_KeepsTheCallersOwnership()
    {
        // Arrange
        var (engine, _) = CreateEngine(new InMemoryPaymentAttemptStore(), new ScriptedProvider(), sessionStore: new OwnerStampingSessionStore("admin-1"));

        // Act
        var session = await engine.StartAsync(new StartCheckoutRequest
        {
            ReferenceType = "Transaction",
            ReferenceId = "tx-1",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("admin-1", session.OwnerId);
    }

    private static CheckoutSession CreateSession()
    {
        var session = new CheckoutSession
        {
            SessionId = "session-1",
            Status = CheckoutSessionStatus.Pending,
            Currency = "USD",
        };

        session.Steps.Add(new CheckoutFlowStep
        {
            Key = CheckoutConstants.PaymentStepKey,
            Order = int.MaxValue,
        });

        session.Put(new CheckoutInvoice
        {
            Currency = "USD",
            InitialPaymentAmount = 25m,
            DueNow = 25m,
            GrandTotal = 25m,
            LineItems = [new CheckoutLineItem { ItemId = "tx-1", Quantity = 1, UnitPrice = 25m }],
        });

        return session;
    }

    private static (DefaultCheckoutEngine Engine, InMemoryCheckoutSessionStore Sessions) CreateEngine(
        InMemoryPaymentAttemptStore attempts,
        ScriptedProvider provider,
        ICheckoutSessionStore sessionStore = null)
    {
        var sessions = new InMemoryCheckoutSessionStore();

        var providerResolver = new Mock<ICheckoutPaymentProviderResolver>();
        providerResolver.Setup(resolver => resolver.GetProvider(ProviderKey)).Returns(provider);

        var distributedLock = new Mock<IDistributedLock>();
        distributedLock
            .Setup(@lock => @lock.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync((new NoopLocker(), true));

        var engine = new DefaultCheckoutEngine(
            sessionStore ?? sessions,
            attempts,
            providerResolver.Object,
            Mock.Of<ICheckoutRecurringPaymentProviderResolver>(),
            new CheckoutReconciliationService(attempts, providerResolver.Object, NullLogger<CheckoutReconciliationService>.Instance),
            Mock.Of<ICheckoutRefundService>(),
            [],
            distributedLock.Object,
            new Mock<ISession>().Object,
            new TestClock(DateTime.UtcNow),
            NullLogger<DefaultCheckoutEngine>.Instance);

        return (engine, sessions);
    }

    private sealed class ScriptedProvider : ICheckoutPaymentProvider
    {
        public PaymentBeginResult Result { get; set; } = PaymentBeginResult.Success("pi_1");

        public string Key => ProviderKey;

        public string DisplayName => ProviderKey;

        public PaymentProviderCapabilities Capabilities { get; } = new()
        {
            SupportsOneTimePayments = true,
            SupportsSavedPaymentMethods = true,
        };

        public Task<PaymentBeginResult> BeginAsync(BeginPaymentContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(Result);

        public Task<PaymentVerificationResult> VerifyAsync(VerifyPaymentContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(new PaymentVerificationResult { Status = PaymentStatus.Unknown });

        public Task<PaymentCancelResult> CancelAsync(CancelPaymentContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(PaymentCancelResult.Success());
    }

    // Stands in for the real store, which stamps the signed-in caller as the owner and issues a guest token before
    // the engine's configuration runs.
    private sealed class OwnerStampingSessionStore : ICheckoutSessionStore
    {
        private readonly string _callerId;

        public OwnerStampingSessionStore(string callerId)
            => _callerId = callerId;

        public Task<CheckoutSession> GetAsync(string sessionId, CancellationToken cancellationToken = default)
            => Task.FromResult<CheckoutSession>(null);

        public Task<CheckoutSession> GetAsync(string sessionId, CheckoutSessionStatus status, CancellationToken cancellationToken = default)
            => Task.FromResult<CheckoutSession>(null);

        public Task<CheckoutSession> GetByReferenceAsync(string referenceType, string referenceId, string referenceVersionId = null, CancellationToken cancellationToken = default)
            => Task.FromResult<CheckoutSession>(null);

        public Task<CheckoutSession> NewAsync(string referenceType, string referenceId, string referenceVersionId = null, Action<CheckoutSession> configure = null, CancellationToken cancellationToken = default)
        {
            var session = new CheckoutSession
            {
                SessionId = "session-new",
                ReferenceType = referenceType,
                ReferenceId = referenceId,
                OwnerId = _callerId,
                GuestTokenHash = "token",
                IPAddress = "127.0.0.1",
            };

            configure?.Invoke(session);

            return Task.FromResult(session);
        }

        public Task<IReadOnlyList<CheckoutSession>> GetStaleAsync(CheckoutSessionStatus status, DateTime modifiedBeforeUtc, int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CheckoutSession>>([]);

        public Task SaveAsync(CheckoutSession session, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class NoopLocker : ILocker
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void Dispose()
        {
        }
    }
}
