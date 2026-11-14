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
/// A payment begun for one total and set aside for another: by an edit (the release service) or by the next begin when
/// the total changed without one. The provider here answers like Stripe: a key it has seen with other parameters is
/// refused, and a payment it was told to cancel is canceled.
/// </summary>
public sealed class DefaultCheckoutEngineSupersedeTests
{
    private const string ProviderKey = "card";

    /// <summary>
    /// M2 (final review): an edit that leaves the total unchanged cancels and supersedes the payment, and the next attempt
    /// got the same idempotency key (only failed attempts counted in the suffix) with its own attempt id in the metadata.
    /// Stripe refuses a key reused with different parameters, so the buyer could not pay until the total changed.
    /// </summary>
    [Fact]
    public async Task AfterAnEditThatKeepsTheTotal_TheNextPaymentGetsItsOwnIdempotencyKey()
    {
        var attempts = new InMemoryPaymentAttemptStore();
        var provider = new StripeLikeProvider();
        var (engine, sessions, release) = Create(attempts, provider);
        sessions.Seed(CreateSession(25m));

        Assert.True((await engine.BeginPaymentAsync("session-1", new BeginPaymentOptions { ProviderKey = ProviderKey }, TestContext.Current.CancellationToken)).Succeeded);
        Assert.Equal(CheckoutPaymentReleaseResult.Released, await release.ReleaseAsync("session-1", TestContext.Current.CancellationToken));

        var outcome = await engine.BeginPaymentAsync("session-1", new BeginPaymentOptions { ProviderKey = ProviderKey }, TestContext.Current.CancellationToken);

        Assert.True(outcome.Succeeded, outcome.ProviderErrorMessage);

        var keys = (await attempts.GetBySessionAsync("session-1", TestContext.Current.CancellationToken)).Select(attempt => attempt.IdempotencyKey).ToArray();

        Assert.Equal(2, keys.Length);
        Assert.Equal(2, keys.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// M1 (final review): when the total changed without the release, the next begin set the old payment aside only
    /// locally. Its client secret stayed live at the provider, and declines against it named a superseded attempt, so
    /// nothing counted them. It is now canceled at the provider first.
    /// </summary>
    [Fact]
    public async Task WhenTheTotalChangedWithoutARelease_TheOldPaymentIsCanceledAtTheProvider()
    {
        var attempts = new InMemoryPaymentAttemptStore();
        var provider = new StripeLikeProvider();
        var (engine, sessions, _) = Create(attempts, provider);
        var session = CreateSession(25m);
        sessions.Seed(session);

        await engine.BeginPaymentAsync("session-1", new BeginPaymentOptions { ProviderKey = ProviderKey }, TestContext.Current.CancellationToken);
        var first = Assert.Single(await attempts.GetBySessionAsync("session-1", TestContext.Current.CancellationToken));

        SetTotal(session, 30m);

        var outcome = await engine.BeginPaymentAsync("session-1", new BeginPaymentOptions { ProviderKey = ProviderKey }, TestContext.Current.CancellationToken);

        Assert.True(outcome.Succeeded, outcome.ProviderErrorMessage);
        Assert.Equal([first.ItemId], provider.CanceledAttemptIds);
        Assert.True(first.IsSuperseded);
    }

    /// <summary>
    /// M1 (final review): a payment for the old total that the provider won't cancel is not set aside, and no second payment
    /// is begun next to it.
    /// </summary>
    [Fact]
    public async Task WhenTheOldPaymentCantBeCanceled_NoSecondPaymentIsBegun()
    {
        var attempts = new InMemoryPaymentAttemptStore();
        var provider = new StripeLikeProvider();
        var (engine, sessions, _) = Create(attempts, provider);
        var session = CreateSession(25m);
        sessions.Seed(session);

        await engine.BeginPaymentAsync("session-1", new BeginPaymentOptions { ProviderKey = ProviderKey }, TestContext.Current.CancellationToken);

        SetTotal(session, 30m);
        provider.CancelSucceeds = false;

        var outcome = await engine.BeginPaymentAsync("session-1", new BeginPaymentOptions { ProviderKey = ProviderKey }, TestContext.Current.CancellationToken);

        Assert.False(outcome.Succeeded);
        Assert.NotNull(outcome.ProviderErrorMessage);
        Assert.Equal(1, provider.BeginCalls);

        var attempt = Assert.Single(await attempts.GetBySessionAsync("session-1", TestContext.Current.CancellationToken));
        Assert.False(attempt.IsSuperseded);
    }

    private static void SetTotal(CheckoutSession session, decimal total)
    {
        var invoice = session.GetOrCreate<CheckoutInvoice>();
        invoice.InitialPaymentAmount = total;
        invoice.DueNow = total;
        invoice.GrandTotal = total;
        invoice.LineItems = [new CheckoutLineItem { ItemId = "tx-1", Quantity = 1, UnitPrice = total }];
        session.Put(invoice);
    }

    private static CheckoutSession CreateSession(decimal total)
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

        session.Put(new CheckoutInvoice { Currency = "USD" });
        SetTotal(session, total);

        return session;
    }

    private static (DefaultCheckoutEngine Engine, InMemoryCheckoutSessionStore Sessions, CheckoutPaymentReleaseService Release) Create(InMemoryPaymentAttemptStore attempts, StripeLikeProvider provider)
    {
        var sessions = new InMemoryCheckoutSessionStore();

        var providerResolver = new Mock<ICheckoutPaymentProviderResolver>();
        providerResolver.Setup(resolver => resolver.GetProvider(ProviderKey)).Returns(provider);

        var distributedLock = new Mock<IDistributedLock>();
        distributedLock
            .Setup(@lock => @lock.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync((new NoopLocker(), true));

        var engine = new DefaultCheckoutEngine(
            sessions,
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

        var release = new CheckoutPaymentReleaseService(sessions, attempts, providerResolver.Object, distributedLock.Object, new Mock<ISession>().Object, NullLogger<CheckoutPaymentReleaseService>.Instance);

        return (engine, sessions, release);
    }

    // Answers like Stripe: a PaymentIntent per idempotency key, and the same key with other parameters (here, another
    // attempt id in the metadata) refused.
    private sealed class StripeLikeProvider : ICheckoutPaymentProvider
    {
        private readonly Dictionary<string, string> _attemptByKey = new(StringComparer.Ordinal);

        public bool CancelSucceeds { get; set; } = true;

        public int BeginCalls { get; private set; }

        public List<string> CanceledAttemptIds { get; } = [];

        public string Key => ProviderKey;

        public string DisplayName => ProviderKey;

        public PaymentProviderCapabilities Capabilities { get; } = new() { SupportsOneTimePayments = true, IsCardNotPresent = true };

        public Task<PaymentBeginResult> BeginAsync(BeginPaymentContext context, CancellationToken cancellationToken = default)
        {
            BeginCalls++;

            if (_attemptByKey.TryGetValue(context.Attempt.IdempotencyKey, out var attemptId) && attemptId != context.Attempt.ItemId)
            {
                return Task.FromResult(PaymentBeginResult.Failure("Keys for idempotent requests can only be used with the same parameters they were first used with."));
            }

            _attemptByKey[context.Attempt.IdempotencyKey] = context.Attempt.ItemId;

            return Task.FromResult(PaymentBeginResult.Success("pi_" + context.Attempt.ItemId));
        }

        public Task<PaymentVerificationResult> VerifyAsync(VerifyPaymentContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(new PaymentVerificationResult { Status = PaymentStatus.Unknown });

        public Task<PaymentCancelResult> CancelAsync(CancelPaymentContext context, CancellationToken cancellationToken = default)
        {
            if (!CancelSucceeds)
            {
                return Task.FromResult(PaymentCancelResult.Failure("The PaymentIntent is processing."));
            }

            CanceledAttemptIds.Add(context.Attempt.ItemId);

            return Task.FromResult(PaymentCancelResult.Success());
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
