using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce;
using CrestApps.OrchardCore.AgenticCommerce.Ap2;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Core.Security;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Models;
using CrestApps.OrchardCore.AgenticCommerce.Services;
using CrestApps.OrchardCore.Carts.Core.Services;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Tests.Checkout;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Locking;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.AgenticCommerce;

/// <summary>
/// SEC-C8, through <see cref="AgentCheckoutService"/>: the AP2 mandate gate runs on every completion that pays, the one
/// that finishes a delegated 3-D Secure payment included, so a checkout is never paid without a mandate for it as it
/// stands, buyer included, and the verified mandate is there for the receipt.
/// </summary>
public sealed class AgentCheckoutCompletionGateTests
{
    private const string Version = "2026-08-25";
    private const string CheckoutId = "chk_test";

    private static readonly DateTime _now = DateTimeOffset.FromUnixTimeSeconds(Ap2TestMandates.Now).UtcDateTime;

    private readonly EcJsonWebKey _userKey = Ap2TestMandates.NewKey("user-1");
    private readonly EcJsonWebKey _agentKey = Ap2TestMandates.NewKey("agent-1");
    private readonly EcJsonWebKey _merchantKey = Ap2TestMandates.NewKey("merchant-1");
    private readonly Mock<ICheckoutEngine> _engine = new();
    private readonly AgentCheckout _checkout;
    private readonly Ap2CompletionGate _gate;
    private readonly AgentCheckoutService _service;

    private int _payments;

    public AgentCheckoutCompletionGateTests()
    {
        _checkout = new AgentCheckout
        {
            ItemId = CheckoutId,
            PlatformId = "platform-1",
            Protocol = AgenticCommerceConstants.Protocols.Ucp,
            Currency = "USD",
            Status = AgentCheckoutStatus.ReadyForComplete,
            Lines = [new AgentCheckoutLineInput { ItemId = "SKU-A", Quantity = 2 }],
            Buyer = new AgentBuyer { Email = "buyer@example.com" },
            CheckoutSessionId = "session-1",
            OrderId = "order-1",
            ExpiresUtc = _now.AddHours(1),
        };
        _checkout.AppliedLines = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(_checkout.Lines, JsonSerializerOptions.Web))));
        _checkout.SetProperty(Ap2CompletionGate.RequiredProperty, "true");
        _checkout.SetProperty(DelegatedAuthentication.AgentRunsThreeDSecureProperty, "true");

        var keys = new Mock<IMerchantSigningKeyService>();

        keys.Setup(service => service.GetPublishedKeysAsync(It.IsAny<CancellationToken>())).ReturnsAsync([Ap2TestMandates.PublicOf(_merchantKey)]);
        _gate = new Ap2CompletionGate(keys.Object, new TestClock(_now));

        // The bank asks for 3-D Secure the first time; the payment with the agent's result goes through.
        _engine.Setup(engine => engine.BeginPaymentAsync(It.IsAny<string>(), It.IsAny<BeginPaymentOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ++_payments == 1
                ? new PaymentBeginOutcome { Declined = true, RequiresAuthentication = true }
                : new PaymentBeginOutcome { Succeeded = true });
        _engine.Setup(engine => engine.TryCompleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckoutCompletionResult { Status = CheckoutCompletionStatus.Completed });

        _service = CreateService();
    }

    [Fact]
    public async Task CompleteAsync_WithAMandateForAnotherCheckout_IsRefusedBeforePaying()
    {
        // Act
        var result = await _service.CompleteAsync(Context(), CheckoutId, Payment(CreateMandate(checkoutId: "chk_other")), update: null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentCheckoutFailure.Refused, result.Failure);
        Assert.Equal(0, _payments);
    }

    [Fact]
    public async Task CompleteAsync_FinishingDelegatedThreeDSecure_WithoutTheMandate_IsRefused()
    {
        // Arrange: the first complete passes the gate and the bank asks for 3-D Secure.
        var first = await _service.CompleteAsync(Context(), CheckoutId, Payment(CreateMandate()), update: null, TestContext.Current.CancellationToken);

        Assert.Equal(AgentCheckoutStatus.AuthenticationRequired, first.View.Status);

        // Act: the agent finishes with the 3-D Secure result and no mandate.
        var result = await _service.CompleteAsync(Context(), CheckoutId, Payment(mandate: null, authenticated: true), update: null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentCheckoutFailure.Refused, result.Failure);
        Assert.Equal(1, _payments);
        Assert.NotEqual(AgentCheckoutStatus.Completed, _checkout.Status);
    }

    [Fact]
    public async Task CompleteAsync_FinishingDelegatedThreeDSecure_WithTheMandate_PaysAndKeepsItForTheReceipt()
    {
        // Arrange
        var mandate = CreateMandate();

        await _service.CompleteAsync(Context(), CheckoutId, Payment(mandate), update: null, TestContext.Current.CancellationToken);

        // Act
        var result = await _service.CompleteAsync(Context(), CheckoutId, Payment(mandate, authenticated: true), update: null, TestContext.Current.CancellationToken);

        // Assert: the gate verified the mandate in this request, so the AP2 extension stores it with the receipt.
        Assert.True(result.Succeeded);
        Assert.Equal(2, _payments);
        Assert.Equal(AgentCheckoutStatus.Completed, _checkout.Status);
        Assert.Equal(mandate, _gate.VerifiedMandate);
    }

    [Fact]
    public async Task CompleteAsync_AfterTheBuyerChangedWhile3DSecureWasPending_IsRefused()
    {
        // Arrange: approved for buyer@example.com; while 3-D Secure is pending the agent changes the e-mail the goods
        // (with no shipping) are delivered to. The update drops the pending request, so the next complete is a new one.
        var mandate = CreateMandate();

        await _service.CompleteAsync(Context(), CheckoutId, Payment(mandate), update: null, TestContext.Current.CancellationToken);
        await _service.UpdateAsync(Context(), CheckoutId, new AgentCheckoutUpdate { Buyer = new AgentBuyer { Email = "someone-else@example.com" } }, TestContext.Current.CancellationToken);

        // Act
        var result = await _service.CompleteAsync(Context(), CheckoutId, Payment(mandate, authenticated: true), update: null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentCheckoutFailure.Refused, result.Failure);
        Assert.Equal(1, _payments);
    }

    private AgentCheckoutContext Context()
        => new()
        {
            PlatformId = "platform-1",
            Protocol = AgenticCommerceConstants.Protocols.Ucp,
            ProtocolVersion = Version,
            PlatformProfile = new JsonObject { ["keys"] = new JsonArray(Ap2TestMandates.PublicOf(_userKey).ToJson()) },
        };

    private static AgentPayment Payment(string mandate, bool authenticated = false)
    {
        var payment = new AgentPayment
        {
            HandlerId = "card",
            Brand = "visa",
            Authentication = authenticated ? ThreeDSecureResultTests.Authenticated() : null,
        };

        if (mandate is not null)
        {
            payment.Properties[Ap2CheckoutExtension.MandateProperty] = mandate;
        }

        return payment;
    }

    // The buyer's mandate over the checkout the store signed: two of SKU-A for 10.00, for buyer@example.com.
    private string CreateMandate(string checkoutId = CheckoutId)
    {
        var approved = Ap2TestMandates.SampleCheckout();

        approved["id"] = checkoutId;
        approved["buyer"] = new JsonObject { ["email"] = "buyer@example.com" };

        var open = Ap2TestMandates.CreateOpenMandate(_userKey, _agentKey);

        return Ap2TestMandates.PresentClosedMandate(open, _agentKey, Ap2TestMandates.CreateCheckoutJwt(approved, _merchantKey));
    }

    private AgentCheckoutService CreateService()
    {
        var store = new Mock<IAgentCheckoutStore>();
        var sessions = new Mock<ICheckoutSessionStore>();
        var orders = new Mock<IOrderStore>();
        var items = new Mock<IAgentItemResolver>();
        var handler = new Mock<IAgentPaymentHandler>();
        var provider = new Mock<ICheckoutPaymentProvider>();
        var providers = new Mock<ICheckoutPaymentProviderResolver>();
        var site = new SiteSettings();
        var siteService = new Mock<ISiteService>();
        var session = new CheckoutSession { SessionId = "session-1", Status = CheckoutSessionStatus.Pending, Currency = "USD" };
        var order = new Order
        {
            ItemId = "order-1",
            Currency = "USD",
            Lines = [new OrderLine { LineId = "li_1", ContentItemId = "product-a", Sku = "SKU-A", Title = "Widget", Quantity = 2, UnitPrice = 5m }],
            Subtotal = 10m,
            Total = 10m,
        };

        store.Setup(s => s.FindByIdAsync(CheckoutId, It.IsAny<CancellationToken>())).ReturnsAsync(_checkout);
        sessions.Setup(s => s.GetAsync("session-1", It.IsAny<CancellationToken>())).ReturnsAsync(session);
        orders.Setup(s => s.FindByIdAsync("order-1", It.IsAny<CancellationToken>())).ReturnsAsync(order);
        items.Setup(s => s.GetItemId(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns("SKU-A");
        handler.Setup(h => h.DescribeAsync(It.IsAny<AgentPaymentHandlerContext>(), It.IsAny<CancellationToken>())).ReturnsAsync(new AgentPaymentHandlerDescriptor { Id = "card", Name = "card" });
        handler.Setup(h => h.PrepareAsync(It.IsAny<AgentPaymentHandlerContext>(), It.IsAny<AgentPayment>(), It.IsAny<CancellationToken>())).ReturnsAsync(new AgentPaymentPreparation { Succeeded = true, ProviderKey = "stripe" });
        provider.Setup(p => p.Capabilities).Returns(new PaymentProviderCapabilities { AcceptsExternalAuthentication = true });
        providers.Setup(p => p.GetProvider("stripe")).Returns(provider.Object);
        site.Put(new AgenticCommerceSettings
        {
            EnableDelegatedAuthentication = true,
            AcquirerBin = "424242",
            AcquirerCountry = "us",
            AcquirerMerchantId = "acq_merchant_1",
            AuthenticationMerchantName = "Example Store",
        });
        siteService.Setup(s => s.GetSiteSettingsAsync()).ReturnsAsync(site);

        var httpContext = new DefaultHttpContext();

        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("store.example.com");

        return new AgentCheckoutService(
            store.Object,
            sessions.Object,
            _engine.Object,
            Mock.Of<ICartService>(),
            Mock.Of<IOrderService>(),
            orders.Object,
            [],
            [],
            [],
            [],
            [handler.Object],
            [_gate],
            providers.Object,
            items.Object,
            siteService.Object,
            new AgentSiteUrls(new HttpContextAccessor { HttpContext = httpContext }, siteService.Object, new SignedTokenService(new EphemeralDataProtectionProvider(), new TestClock(_now))),
            new LocalLock(NullLogger<LocalLock>.Instance),
            Mock.Of<global::YesSql.ISession>(),
            new TestClock(_now),
            NullLogger<AgentCheckoutService>.Instance,
            new PassThroughLocalizer());
    }

    private sealed class PassThroughLocalizer : IStringLocalizer<AgentCheckoutService>
    {
        public LocalizedString this[string name]
            => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => [];
    }
}
