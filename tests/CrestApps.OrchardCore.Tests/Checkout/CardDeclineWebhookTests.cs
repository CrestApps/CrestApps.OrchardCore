using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Handlers;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Core.Throttling;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using CrestApps.OrchardCore.Stripe.Services;
using CrestApps.OrchardCore.Tests.Checkout.Fakes;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Throttling;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Settings;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// SEC-C7 / RL-4 (review): with Stripe the card is confirmed in the browser against a client secret, so a decline
/// happens at Stripe and comes back only as a <c>payment_intent.payment_failed</c> webhook (or, for a subscription's first
/// payment, <c>invoice.payment_failed</c>). Neither the begin nor the reconciliation sweep sees it, and a tester could try
/// card after card against one secret with nothing counted. The webhook's failure event now counts, and after a few
/// failures, or once the network is blocked, the payment is canceled so a new begin is needed. The cancel goes through the
/// real Stripe provider against a PaymentIntent double that answers like Stripe: a succeeded intent can't be canceled.
/// </summary>
public sealed class CardDeclineWebhookTests
{
    private const string CardProvider = StripeConstants.ProcessorKey;
    private const string Network = "203.0.113.7";

    private readonly IDistributedCache _cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
    private readonly InMemoryPaymentAttemptStore _attempts = new();
    private readonly InMemoryCheckoutSessionStore _sessions = new();
    private readonly List<PaymentIssue> _issues = [];
    private readonly Dictionary<string, string> _intentStatuses = new(StringComparer.Ordinal);
    private readonly List<string> _canceledIntents = [];
    private readonly List<CreateRefundRequest> _refunds = [];
    private readonly List<CancelSubscriptionRequest> _canceledSubscriptions = [];

    [Fact]
    public async Task CardsDeclinedAgainstOneClientSecret_AreCounted_UntilThePaymentIsCanceled_AndTheGateRefusesTheNetwork()
    {
        var (handler, gate) = Create();
        var session = Seed("session-1");

        // Begin returned a client secret; the browser confirmed card after card and Stripe declined each.
        await SeedAttemptAsync("attempt-1", "pi_1", session);

        for (var i = 0; i < 3; i++)
        {
            await handler.PaymentFailedAsync(Failed("attempt-1", "pi_1"), TestContext.Current.CancellationToken);
        }

        var first = await _attempts.FindByIdAsync("attempt-1", TestContext.Current.CancellationToken);

        Assert.Equal(["pi_1"], _canceledIntents);
        Assert.Equal(PaymentAttemptState.Failed, first.State);

        // The client secret is dead; a new begin gets a new one, and two more declines block the network.
        await SeedAttemptAsync("attempt-2", "pi_2", session);
        await handler.PaymentFailedAsync(Failed("attempt-2", "pi_2"), TestContext.Current.CancellationToken);
        await handler.PaymentFailedAsync(Failed("attempt-2", "pi_2"), TestContext.Current.CancellationToken);

        var next = new CheckoutPaymentBeginningContext(new CheckoutFlow(Seed("session-2")), CardProvider);
        await gate.BeginningAsync(next, TestContext.Current.CancellationToken);

        Assert.True(next.IsRefused);
        Assert.Single(_issues, issue => issue.Kind == PaymentIssueKinds.SuspectedCardTesting);
    }

    [Fact]
    public async Task AFailureEventForAFinishedPayment_CountsNothing()
    {
        var (handler, _) = Create();
        var session = Seed("session-1");

        await SeedAttemptAsync("attempt-1", "pi_1", session, PaymentAttemptState.Succeeded);

        for (var i = 0; i < 6; i++)
        {
            await handler.PaymentFailedAsync(Failed("attempt-1", "pi_1"), TestContext.Current.CancellationToken);
        }

        Assert.Empty(_canceledIntents);
        Assert.Empty(_issues);
    }

    [Fact]
    public async Task ALateDeclineEvent_ForAPaymentTheBuyerPaidWithALaterCard_NeverRefundsIt()
    {
        var (handler, _) = Create();
        var session = Seed("session-1");

        // Three cards declined, then the fourth succeeded on the same PaymentIntent. The success is not processed yet, so
        // the attempt is still pending locally, and Stripe's retry of a decline event arrives now.
        await SeedAttemptAsync("attempt-1", "pi_1", session);
        await handler.PaymentFailedAsync(Failed("attempt-1", "pi_1", "evt_1"), TestContext.Current.CancellationToken);
        await handler.PaymentFailedAsync(Failed("attempt-1", "pi_1", "evt_2"), TestContext.Current.CancellationToken);
        _intentStatuses["pi_1"] = "succeeded";
        await handler.PaymentFailedAsync(Failed("attempt-1", "pi_1", "evt_3"), TestContext.Current.CancellationToken);

        var attempt = await _attempts.FindByIdAsync("attempt-1", TestContext.Current.CancellationToken);

        Assert.Empty(_refunds);
        Assert.Empty(_canceledIntents);
        Assert.Equal(PaymentAttemptState.Pending, attempt.State);
    }

    [Fact]
    public async Task ADeclineCounterThatCannotBeLocked_DoesNotCancelThePaymentAfterOneDecline()
    {
        // The attempt counter's lock can't be taken (contention, or the lock store is down); the checkout lock can.
        var (handler, _) = Create(new RefusingLock(":" + CheckoutConstants.AttemptPolicies.PaymentDeclineAttempt + ":"));
        var session = Seed("session-1");

        await SeedAttemptAsync("attempt-1", "pi_1", session);
        await handler.PaymentFailedAsync(Failed("attempt-1", "pi_1"), TestContext.Current.CancellationToken);

        var attempt = await _attempts.FindByIdAsync("attempt-1", TestContext.Current.CancellationToken);

        Assert.Empty(_canceledIntents);
        Assert.Equal(PaymentAttemptState.Pending, attempt.State);
    }

    [Fact]
    public async Task AnEventStripeDeliversAgain_CountsItsDeclineOnce()
    {
        var (handler, _) = Create();
        var session = Seed("session-1");

        await SeedAttemptAsync("attempt-1", "pi_1", session);

        // The first delivery of evt_1 counted, then a later handler threw, so the webhook answered 500 and Stripe sent it
        // again. Two declines in all: the payment stays open for a third card.
        await handler.PaymentFailedAsync(Failed("attempt-1", "pi_1", "evt_1"), TestContext.Current.CancellationToken);
        await handler.PaymentFailedAsync(Failed("attempt-1", "pi_1", "evt_1"), TestContext.Current.CancellationToken);
        await handler.PaymentFailedAsync(Failed("attempt-1", "pi_1", "evt_2"), TestContext.Current.CancellationToken);

        var attempt = await _attempts.FindByIdAsync("attempt-1", TestContext.Current.CancellationToken);

        Assert.Empty(_canceledIntents);
        Assert.Equal(PaymentAttemptState.Pending, attempt.State);
    }

    [Fact]
    public async Task CardsDeclinedOnASubscriptionsFirstInvoice_AreCounted_AndTheSubscriptionAndItsInvoiceEnd()
    {
        var (handler, _) = Create();
        var session = Seed("session-1");

        // A subscription checkout's attempt holds the subscription; Stripe reports each declined card on its first invoice
        // as invoice.payment_failed, with the subscription's metadata.
        await SeedAttemptAsync("attempt-1", "sub_1", session, obligationId: "recurring:month:1");

        for (var i = 0; i < 3; i++)
        {
            var failed = new SubscriptionPaymentFailedContext { SubscriptionId = "sub_1", TransactionId = "in_1", GatewayId = CardProvider, EventId = $"evt_{i}" };
            failed.Data[CardDeclinePaymentEventHandler.AttemptIdDataKey] = "attempt-1";

            await handler.SubscriptionPaymentFailedAsync(failed, TestContext.Current.CancellationToken);
        }

        var attempt = await _attempts.FindByIdAsync("attempt-1", TestContext.Current.CancellationToken);
        var canceled = Assert.Single(_canceledSubscriptions);

        Assert.Equal("sub_1", canceled.SubscriptionId);
        Assert.True(canceled.VoidOpenInvoice);
        Assert.Equal(PaymentAttemptState.Failed, attempt.State);
    }

    [Fact]
    public async Task OnceANetworkIsBlocked_APaymentItBeganEarlier_IsCanceledAtItsFirstDecline()
    {
        var (handler, _) = Create();

        // One network began six checkouts and holds six client secrets, then tries one card on each.
        for (var i = 1; i <= 6; i++)
        {
            await SeedAttemptAsync($"attempt-{i}", $"pi_{i}", Seed($"session-{i}"));
        }

        for (var i = 1; i <= 6; i++)
        {
            await handler.PaymentFailedAsync(Failed($"attempt-{i}", $"pi_{i}"), TestContext.Current.CancellationToken);
        }

        // The fifth decline blocked the network: that payment and the sixth are canceled after one card each, instead of
        // taking three cards apiece.
        Assert.Equal(["pi_5", "pi_6"], _canceledIntents);
        Assert.Equal(PaymentAttemptState.Pending, (await _attempts.FindByIdAsync("attempt-4", TestContext.Current.CancellationToken)).State);
        Assert.Equal(PaymentAttemptState.Failed, (await _attempts.FindByIdAsync("attempt-6", TestContext.Current.CancellationToken)).State);
    }

    /// <summary>
    /// M1 (final review): a payment set aside when the total changed kept its client secret at Stripe, and each decline
    /// against it named a superseded attempt, so nothing was counted and the payment was never canceled. A decline that
    /// names a superseded attempt and its own PaymentIntent now counts, and cancels the payment while it is unpaid.
    /// </summary>
    [Fact]
    public async Task DeclinesAgainstASupersededPayment_AreCounted_AndItsClientSecretIsCanceled()
    {
        var (handler, gate) = Create();

        for (var i = 1; i <= 5; i++)
        {
            await SeedAttemptAsync($"attempt-{i}", $"pi_{i}", Seed($"session-{i}"), PaymentAttemptState.Canceled, isSuperseded: true);
            await handler.PaymentFailedAsync(Failed($"attempt-{i}", $"pi_{i}"), TestContext.Current.CancellationToken);
        }

        Assert.Equal(["pi_1", "pi_2", "pi_3", "pi_4", "pi_5"], _canceledIntents);
        Assert.True((await _attempts.FindByIdAsync("attempt-1", TestContext.Current.CancellationToken)).IsSuperseded);

        var next = new CheckoutPaymentBeginningContext(new CheckoutFlow(Seed("session-next")), CardProvider);
        await gate.BeginningAsync(next, TestContext.Current.CancellationToken);

        Assert.True(next.IsRefused);
    }

    [Fact]
    public async Task ADeclineNamingASupersededAttempt_ForAnotherPaymentIntent_CountsNothing()
    {
        var (handler, _) = Create();

        await SeedAttemptAsync("attempt-1", "pi_1", Seed("session-1"), PaymentAttemptState.Canceled, isSuperseded: true);
        _intentStatuses["pi_other"] = "requires_payment_method";

        for (var i = 0; i < 6; i++)
        {
            await handler.PaymentFailedAsync(Failed("attempt-1", "pi_other"), TestContext.Current.CancellationToken);
        }

        Assert.Empty(_canceledIntents);
        Assert.Empty(_issues);
    }

    private static PaymentFailedContext Failed(string attemptId, string intentId, string eventId = null)
    {
        var context = new PaymentFailedContext { TransactionId = intentId, GatewayId = CardProvider, FailureCode = "card_declined", EventId = eventId ?? Guid.NewGuid().ToString("n") };
        context.Data[CardDeclinePaymentEventHandler.AttemptIdDataKey] = attemptId;

        return context;
    }

    private CheckoutSession Seed(string sessionId)
    {
        var session = new CheckoutSession { SessionId = sessionId, Status = CheckoutSessionStatus.AwaitingProvider, Currency = "USD" };
        session.Put(new CheckoutClientInfo { NetworkKey = ClientNetworkKey.FromAddress(Network) });
        _sessions.Seed(session);

        return session;
    }

    private Task SeedAttemptAsync(string attemptId, string reference, CheckoutSession session, PaymentAttemptState state = PaymentAttemptState.Pending, string obligationId = CheckoutObligations.OneTime, bool isSuperseded = false)
    {
        // After a decline Stripe leaves the PaymentIntent waiting for another card.
        _intentStatuses[reference] = "requires_payment_method";

        return _attempts.CreateAsync(new PaymentAttempt
        {
            ItemId = attemptId,
            SessionId = session.SessionId,
            ProviderKey = CardProvider,
            ObligationId = obligationId,
            ProviderReference = reference,
            ExpectedAmount = 30m,
            Currency = "USD",
            State = state,
            IsSuperseded = isSuperseded,
        }).AsTask();
    }

    private (IPaymentEvent Handler, PaymentVelocityGate Gate) Create(IDistributedLock distributedLock = null)
    {
        distributedLock ??= new LocalLock(NullLogger<LocalLock>.Instance);

        var options = new AttemptLimitOptions();
        options.AddPolicy(CheckoutConstants.AttemptPolicies.PaymentBeginCheckout, 8, TimeSpan.FromHours(24));
        options.AddPolicy(CheckoutConstants.AttemptPolicies.PaymentDeclineNetwork, 5, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30));
        options.AddPolicy(CheckoutConstants.AttemptPolicies.PaymentDeclineCustomer, 5, TimeSpan.FromHours(1), TimeSpan.FromHours(1));
        options.AddPolicy(CheckoutConstants.AttemptPolicies.PaymentDeclineStore, 30, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(30));
        options.AddPolicy(CheckoutConstants.AttemptPolicies.PaymentDeclineAttempt, 3, TimeSpan.FromHours(24), TimeSpan.FromHours(24));
        options.AddPolicy(CheckoutConstants.AttemptPolicies.PaymentDeclineEvent, 1, TimeSpan.FromDays(3));

        var clock = new TestClock(DateTime.UtcNow);
        var limiter = new DistributedAttemptLimiter(_cache, distributedLock, new ShellSettings { Name = "Default" }, clock, Options.Create(options), NullLogger<DistributedAttemptLimiter>.Instance);

        var resolver = new Mock<ICheckoutPaymentProviderResolver>();
        resolver.Setup(r => r.GetProvider(CardProvider)).Returns(CreateStripeProvider(clock));

        var issues = new Mock<IPaymentIssueRecorder>();
        issues.Setup(i => i.RecordAsync(It.IsAny<PaymentIssue>(), It.IsAny<CancellationToken>()))
            .Callback<PaymentIssue, CancellationToken>((issue, _) => _issues.Add(issue))
            .ReturnsAsync(true);

        var site = new Mock<ISite>();
        site.Setup(s => s.GetOrCreate<PaymentProtectionSettings>()).Returns(new PaymentProtectionSettings());
        var siteService = new Mock<ISiteService>();
        siteService.Setup(s => s.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        var services = new ServiceCollection()
            .AddSingleton<IAttemptLimiter>(limiter)
            .AddSingleton(resolver.Object)
            .AddSingleton(issues.Object)
            .AddSingleton(Options.Create(options))
            .AddSingleton<IPaymentAttemptStore>(_attempts)
            .AddSingleton<ICheckoutSessionStore>(_sessions)
            .AddSingleton(distributedLock)
            .AddSingleton(siteService.Object)
            .AddSingleton<IStringLocalizer<PaymentVelocityGate>>(new PassThroughStringLocalizer<PaymentVelocityGate>())
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddSingleton<IPaymentDeclineRecorder>(provider => ActivatorUtilities.CreateInstance<PaymentDeclineRecorder>(provider))
            .BuildServiceProvider();

        return (ActivatorUtilities.CreateInstance<CardDeclinePaymentEventHandler>(services), ActivatorUtilities.CreateInstance<PaymentVelocityGate>(services));
    }

    // The real Stripe provider over doubles that answer as Stripe does: a PaymentIntent can be canceled in any state but
    // succeeded or canceled, and a refund is recorded so a test can see one was asked for.
    private StripeCheckoutPaymentProvider CreateStripeProvider(TestClock clock)
    {
        var intents = new Mock<IStripePaymentIntentService>();
        intents.Setup(service => service.RetrieveAsync(It.IsAny<RetrievePaymentIntentRequest>()))
            .ReturnsAsync((RetrievePaymentIntentRequest request) => new PaymentIntentDetails
            {
                Id = request.PaymentIntentId,
                Status = _intentStatuses[request.PaymentIntentId],
                Amount = 3000,
                AmountReceived = _intentStatuses[request.PaymentIntentId] == "succeeded" ? 3000 : 0,
                Currency = "usd",
            });
        intents.Setup(service => service.CancelAsync(It.IsAny<CancelPaymentIntentRequest>()))
            .ReturnsAsync((CancelPaymentIntentRequest request) =>
            {
                if (_intentStatuses[request.PaymentIntentId] is "succeeded" or "canceled")
                {
                    throw new InvalidOperationException($"You cannot cancel this PaymentIntent because it has a status of {_intentStatuses[request.PaymentIntentId]}.");
                }

                _intentStatuses[request.PaymentIntentId] = "canceled";
                _canceledIntents.Add(request.PaymentIntentId);

                return new PaymentIntentDetails { Id = request.PaymentIntentId, Status = "canceled" };
            });

        var refunds = new Mock<IStripeRefundService>();
        refunds.Setup(service => service.CreateAsync(It.IsAny<CreateRefundRequest>()))
            .ReturnsAsync((CreateRefundRequest request) =>
            {
                _refunds.Add(request);

                return new RefundDetails { Id = "re_1", Status = "succeeded", Amount = 3000, Currency = "usd" };
            });

        var subscriptions = new Mock<IStripeSubscriptionService>();
        subscriptions.Setup(service => service.GetAsync(It.IsAny<string>()))
            .ReturnsAsync((string id) => new SubscriptionDetails { Id = id, Status = _intentStatuses[id] == "canceled" ? "canceled" : "incomplete" });
        subscriptions.Setup(service => service.CancelAsync(It.IsAny<CancelSubscriptionRequest>()))
            .ReturnsAsync((CancelSubscriptionRequest request) =>
            {
                _intentStatuses[request.SubscriptionId] = "canceled";
                _canceledSubscriptions.Add(request);

                return new SubscriptionDetails { Id = request.SubscriptionId, Status = "canceled" };
            });

        return new StripeCheckoutPaymentProvider(
            intents.Object,
            Mock.Of<IStripePaymentMethodService>(),
            subscriptions.Object,
            refunds.Object,
            Mock.Of<IStripeCheckoutCustomerResolver>(),
            Mock.Of<IStripeWalletService>(),
            new InMemoryPaymentRefundStore(),
            clock,
            NullLogger<StripeCheckoutPaymentProvider>.Instance,
            new PassThroughStringLocalizer<StripeCheckoutPaymentProvider>());
    }

    // A lock store that can't give out the locks whose key contains a fragment, and gives out every other one.
    private sealed class RefusingLock : IDistributedLock
    {
        private readonly string _refusedFragment;
        private readonly LocalLock _inner = new(NullLogger<LocalLock>.Instance);

        public RefusingLock(string refusedFragment)
            => _refusedFragment = refusedFragment;

        public Task<ILocker> AcquireLockAsync(string key, TimeSpan? expiration = null)
            => _inner.AcquireLockAsync(key, expiration);

        public Task<(ILocker locker, bool locked)> TryAcquireLockAsync(string key, TimeSpan timeout, TimeSpan? expiration = null)
            => key.Contains(_refusedFragment, StringComparison.Ordinal)
                ? Task.FromResult<(ILocker, bool)>((null, false))
                : _inner.TryAcquireLockAsync(key, timeout, expiration);

        public Task<bool> IsLockAcquiredAsync(string key)
            => _inner.IsLockAcquiredAsync(key);
    }
}
