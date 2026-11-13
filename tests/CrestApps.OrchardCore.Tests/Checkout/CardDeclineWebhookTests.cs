using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Handlers;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Core.Throttling;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Tests.Checkout.Fakes;
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
using OrchardCore.Modules;
using OrchardCore.Settings;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// SEC-C7 / RL-4 (review): with Stripe the card is confirmed in the browser against a client secret, so a decline
/// happens at Stripe and comes back only as a <c>payment_intent.payment_failed</c> webhook. Neither the begin nor the
/// reconciliation sweep sees it, and a tester could try card after card against one secret with nothing counted. The
/// webhook's failure event now counts, and after a few failures the payment is canceled so a new begin is needed.
/// </summary>
public sealed class CardDeclineWebhookTests
{
    private const string CardProvider = "stripe";
    private const string Network = "203.0.113.7";

    private readonly IDistributedCache _cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
    private readonly InMemoryPaymentAttemptStore _attempts = new();
    private readonly InMemoryCheckoutSessionStore _sessions = new();
    private readonly List<PaymentIssue> _issues = [];
    private int _cancels;

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

        Assert.Equal(1, _cancels);
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

        Assert.Equal(0, _cancels);
        Assert.Empty(_issues);
    }

    private static PaymentFailedContext Failed(string attemptId, string intentId)
    {
        var context = new PaymentFailedContext { TransactionId = intentId, GatewayId = CardProvider, FailureCode = "card_declined" };
        context.Data["checkout_attempt_id"] = attemptId;

        return context;
    }

    private CheckoutSession Seed(string sessionId)
    {
        var session = new CheckoutSession { SessionId = sessionId, Status = CheckoutSessionStatus.AwaitingProvider, Currency = "USD" };
        session.Put(new CheckoutClientInfo { NetworkKey = ClientNetworkKey.FromAddress(Network) });
        _sessions.Seed(session);

        return session;
    }

    private Task SeedAttemptAsync(string attemptId, string intentId, CheckoutSession session, PaymentAttemptState state = PaymentAttemptState.Pending)
        => _attempts.CreateAsync(new PaymentAttempt
        {
            ItemId = attemptId,
            SessionId = session.SessionId,
            ProviderKey = CardProvider,
            ObligationId = CheckoutObligations.OneTime,
            ProviderReference = intentId,
            ExpectedAmount = 30m,
            Currency = "USD",
            State = state,
        }).AsTask();

    private (IPaymentEvent Handler, PaymentVelocityGate Gate) Create()
    {
        var options = new AttemptLimitOptions();
        options.AddPolicy(CheckoutConstants.AttemptPolicies.PaymentBeginCheckout, 8, TimeSpan.FromHours(24));
        options.AddPolicy(CheckoutConstants.AttemptPolicies.PaymentDeclineNetwork, 5, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30));
        options.AddPolicy(CheckoutConstants.AttemptPolicies.PaymentDeclineCustomer, 5, TimeSpan.FromHours(1), TimeSpan.FromHours(1));
        options.AddPolicy(CheckoutConstants.AttemptPolicies.PaymentDeclineStore, 30, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(30));
        options.AddPolicy("payment-decline-attempt", 3, TimeSpan.FromHours(24), TimeSpan.FromHours(24));

        var clock = Mock.Of<IClock>(value => value.UtcNow == DateTime.UtcNow);
        var limiter = new DistributedAttemptLimiter(_cache, new LocalLock(NullLogger<LocalLock>.Instance), new ShellSettings { Name = "Default" }, clock, Options.Create(options), NullLogger<DistributedAttemptLimiter>.Instance);

        var provider = new Mock<ICheckoutPaymentProvider>();
        provider.SetupGet(p => p.Key).Returns(CardProvider);
        provider.SetupGet(p => p.Capabilities).Returns(new PaymentProviderCapabilities { SupportsOneTimePayments = true, IsCardNotPresent = true });
        provider.Setup(p => p.CancelAsync(It.IsAny<CancelPaymentContext>(), It.IsAny<CancellationToken>()))
            .Callback(() => _cancels++)
            .ReturnsAsync(PaymentCancelResult.Success());

        var resolver = new Mock<ICheckoutPaymentProviderResolver>();
        resolver.Setup(r => r.GetProvider(CardProvider)).Returns(provider.Object);

        var issues = new Mock<IPaymentIssueRecorder>();
        issues.Setup(i => i.RecordAsync(It.IsAny<PaymentIssue>(), It.IsAny<CancellationToken>()))
            .Callback<PaymentIssue, CancellationToken>((issue, _) => _issues.Add(issue))
            .ReturnsAsync(true);

        var site = new Mock<ISite>();
        site.Setup(s => s.GetOrCreate<PaymentProtectionSettings>()).Returns(new PaymentProtectionSettings());
        var siteService = new Mock<ISiteService>();
        siteService.Setup(s => s.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        var localizer = new Mock<IStringLocalizer<PaymentVelocityGate>>();
        localizer.Setup(l => l[It.IsAny<string>()]).Returns((string name) => new LocalizedString(name, name));

        var services = new ServiceCollection()
            .AddSingleton<IAttemptLimiter>(limiter)
            .AddSingleton(resolver.Object)
            .AddSingleton(issues.Object)
            .AddSingleton(Options.Create(options))
            .AddSingleton<IPaymentAttemptStore>(_attempts)
            .AddSingleton<ICheckoutSessionStore>(_sessions)
            .AddSingleton(siteService.Object)
            .AddSingleton(localizer.Object)
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddSingleton<IPaymentDeclineRecorder>(provider => ActivatorUtilities.CreateInstance<PaymentDeclineRecorder>(provider))
            .BuildServiceProvider();

        return (ActivatorUtilities.CreateInstance<CardDeclinePaymentEventHandler>(services), ActivatorUtilities.CreateInstance<PaymentVelocityGate>(services));
    }
}
