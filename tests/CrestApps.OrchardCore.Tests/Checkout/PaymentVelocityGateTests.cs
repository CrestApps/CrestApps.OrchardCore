using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Handlers;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Core.Throttling;
using CrestApps.OrchardCore.Throttling;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking;
using OrchardCore.Modules;
using OrchardCore.Settings;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// RL-4 (SEC-C7): a card tester runs stolen cards through checkouts. The gate stops a network or a customer once their
/// declines reach the limit, before any provider is asked; every decline is counted once; and a store-wide run of
/// declines (testers rotating networks) triggers the store's chosen action.
/// </summary>
public sealed class PaymentVelocityGateTests
{
    private const string CardProvider = "card";
    private const string CashProvider = "cash";

    private readonly IDistributedCache _cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
    private readonly List<PaymentIssue> _issues = [];

    [Fact]
    public async Task AfterFiveDeclinesFromOneNetwork_TheGateRefuses()
    {
        var (gate, recorder) = Create();

        for (var i = 0; i < 5; i++)
        {
            await recorder.RecordAsync(Session($"s{i}", network: "203.0.113.7"), Attempt(CardProvider), TestContext.Current.CancellationToken);
        }

        var context = Context(Session("s-next", network: "203.0.113.7"), CardProvider);
        await gate.BeginningAsync(context, TestContext.Current.CancellationToken);

        Assert.True(context.IsRefused);
    }

    [Fact]
    public async Task AnotherNetwork_IsNotAffected()
    {
        var (gate, recorder) = Create();

        for (var i = 0; i < 5; i++)
        {
            await recorder.RecordAsync(Session($"s{i}", network: "203.0.113.7"), Attempt(CardProvider), TestContext.Current.CancellationToken);
        }

        var context = Context(Session("s-other", network: "198.51.100.9"), CardProvider);
        await gate.BeginningAsync(context, TestContext.Current.CancellationToken);

        Assert.False(context.IsRefused);
    }

    [Fact]
    public async Task APaymentMethodThatTakesNoCardOnline_SkipsTheGate()
    {
        var (gate, recorder) = Create();

        for (var i = 0; i < 5; i++)
        {
            await recorder.RecordAsync(Session($"s{i}", network: "203.0.113.7"), Attempt(CardProvider), TestContext.Current.CancellationToken);
        }

        var context = Context(Session("s-cash", network: "203.0.113.7"), CashProvider);
        await gate.BeginningAsync(context, TestContext.Current.CancellationToken);

        Assert.False(context.IsRefused);
    }

    [Fact]
    public async Task TheBlock_RecordsOnePaymentIssue()
    {
        var (_, recorder) = Create();

        for (var i = 0; i < 8; i++)
        {
            await recorder.RecordAsync(Session($"s{i}", network: "203.0.113.7"), Attempt(CardProvider), TestContext.Current.CancellationToken);
        }

        Assert.Single(_issues, issue => issue.Kind == PaymentIssueKinds.SuspectedCardTesting);
    }

    [Fact]
    public async Task AGuestsDeclines_DoNotBlockACustomer()
    {
        var (gate, recorder) = Create();

        for (var i = 0; i < 5; i++)
        {
            await recorder.RecordAsync(Session($"s{i}", network: $"203.0.113.{i + 10}"), Attempt(CardProvider), TestContext.Current.CancellationToken);
        }

        var context = Context(Session("s-customer", network: "198.51.100.9", ownerId: "customer-1"), CardProvider);
        await gate.BeginningAsync(context, TestContext.Current.CancellationToken);

        Assert.False(context.IsRefused);
    }

    [Theory]
    [InlineData(DeclineSpikeAction.Notify, false, false)]
    [InlineData(DeclineSpikeAction.RequireSignIn, true, false)]
    [InlineData(DeclineSpikeAction.PauseGuestCards, true, false)]
    public async Task AStoreWideRunOfDeclines_AppliesTheStoresAction(DeclineSpikeAction action, bool guestRefused, bool customerRefused)
    {
        var (gate, recorder) = Create(action);

        // Thirty declines from thirty networks: no network is blocked, but the store is.
        for (var i = 0; i < 30; i++)
        {
            await recorder.RecordAsync(Session($"s{i}", network: $"10.1.{i / 250}.{i % 250 + 1}"), Attempt(CardProvider), TestContext.Current.CancellationToken);
        }

        Assert.Single(_issues, issue => issue.Kind == PaymentIssueKinds.DeclineSpike);

        var guest = Context(Session("guest", network: "198.51.100.1"), CardProvider);
        var customer = Context(Session("customer", network: "198.51.100.2", ownerId: "customer-2"), CardProvider);

        await gate.BeginningAsync(guest, TestContext.Current.CancellationToken);
        await gate.BeginningAsync(customer, TestContext.Current.CancellationToken);

        Assert.Equal(guestRefused, guest.IsRefused);
        Assert.Equal(customerRefused, customer.IsRefused);
    }

    [Fact]
    public void ADeclineIsCountedOnce_OnTheTransitionToFailed()
    {
        Assert.True(PaymentDeclineRecorder.IsNewDecline(PaymentAttemptState.Pending, PaymentAttemptState.Failed));
        Assert.False(PaymentDeclineRecorder.IsNewDecline(PaymentAttemptState.Failed, PaymentAttemptState.Failed));
        Assert.False(PaymentDeclineRecorder.IsNewDecline(PaymentAttemptState.Pending, PaymentAttemptState.Succeeded));
    }

    private (PaymentVelocityGate Gate, PaymentDeclineRecorder Recorder) Create(DeclineSpikeAction action = DeclineSpikeAction.Notify)
    {
        var options = new AttemptLimitOptions();
        options.AddPolicy(CheckoutConstants.AttemptPolicies.PaymentBeginNetwork, 30, TimeSpan.FromMinutes(10));
        options.AddPolicy(CheckoutConstants.AttemptPolicies.PaymentBeginCheckout, 8, TimeSpan.FromHours(24));
        options.AddPolicy(CheckoutConstants.AttemptPolicies.PaymentDeclineNetwork, 5, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30));
        options.AddPolicy(CheckoutConstants.AttemptPolicies.PaymentDeclineCustomer, 5, TimeSpan.FromHours(1), TimeSpan.FromHours(1));
        options.AddPolicy(CheckoutConstants.AttemptPolicies.PaymentDeclineStore, 30, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(30));

        var clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(() => DateTime.UtcNow);

        var limiter = new DistributedAttemptLimiter(
            _cache,
            new LocalLock(NullLogger<LocalLock>.Instance),
            new ShellSettings { Name = "Default" },
            clock.Object,
            Options.Create(options),
            NullLogger<DistributedAttemptLimiter>.Instance);

        var resolver = new Mock<ICheckoutPaymentProviderResolver>();
        resolver.Setup(r => r.GetProvider(CardProvider)).Returns(Provider(CardProvider, cardNotPresent: true));
        resolver.Setup(r => r.GetProvider(CashProvider)).Returns(Provider(CashProvider, cardNotPresent: false));

        var site = new Mock<ISite>();
        site.Setup(s => s.GetOrCreate<PaymentProtectionSettings>()).Returns(new PaymentProtectionSettings { OnDeclineSpike = action });
        var siteService = new Mock<ISiteService>();
        siteService.Setup(s => s.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        var issues = new Mock<IPaymentIssueRecorder>();
        issues.Setup(i => i.RecordAsync(It.IsAny<PaymentIssue>(), It.IsAny<CancellationToken>()))
            .Callback<PaymentIssue, CancellationToken>((issue, _) => _issues.Add(issue))
            .ReturnsAsync(true);

        var localizer = new Mock<IStringLocalizer<PaymentVelocityGate>>();
        localizer.Setup(l => l[It.IsAny<string>()]).Returns((string name) => new LocalizedString(name, name));

        var gate = new PaymentVelocityGate(limiter, resolver.Object, siteService.Object, localizer.Object);
        var recorder = new PaymentDeclineRecorder(limiter, resolver.Object, issues.Object, NullLogger<PaymentDeclineRecorder>.Instance);

        return (gate, recorder);
    }

    private static ICheckoutPaymentProvider Provider(string key, bool cardNotPresent)
    {
        var provider = new Mock<ICheckoutPaymentProvider>();
        provider.SetupGet(p => p.Key).Returns(key);
        provider.SetupGet(p => p.Capabilities).Returns(new PaymentProviderCapabilities { SupportsOneTimePayments = true, IsCardNotPresent = cardNotPresent });

        return provider.Object;
    }

    private static CheckoutSession Session(string sessionId, string network, string ownerId = null)
    {
        var session = new CheckoutSession { SessionId = sessionId, OwnerId = ownerId, Currency = "USD" };
        session.Put(new CheckoutClientInfo { NetworkKey = ClientNetworkKey.FromAddress(network) });

        return session;
    }

    private static PaymentAttempt Attempt(string providerKey)
        => new() { ItemId = Guid.NewGuid().ToString("N"), ProviderKey = providerKey, State = PaymentAttemptState.Failed };

    private static CheckoutPaymentBeginningContext Context(CheckoutSession session, string providerKey)
        => new(new CheckoutFlow(session), providerKey);
}
