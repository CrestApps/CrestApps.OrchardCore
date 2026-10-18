using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Services;
using CrestApps.OrchardCore.Tests.Customers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Email;
using OrchardCore.Entities;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Carts;

/// <summary>
/// A buyer who left a checkout unpaid gets one email with what they chose and a signed link back, and only when they
/// left on their own a while ago and haven't paid since.
/// </summary>
public sealed class CheckoutRecoveryServiceTests
{
    private static readonly DateTime _now = new(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryOrderStore _orders = new();
    private readonly Mock<IEmailService> _email = new();
    private readonly Dictionary<string, CheckoutSession> _sessions = [];
    private readonly List<MailMessage> _sent = [];
    private readonly EphemeralDataProtectionProvider _protection = new();

    public CheckoutRecoveryServiceTests()
    {
        _email
            .Setup(email => email.SendAsync(It.IsAny<MailMessage>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<MailMessage, string, CancellationToken>((message, _, _) => _sent.Add(message));
    }

    [Fact]
    public async Task EmailsTheContactOnTheCheckout_Once()
    {
        var order = LeftOrder("order-1", "session-1");
        AddSession("session-1", "Jordan Guest", "jordan@example.test");

        var service = CreateService();

        Assert.Equal(1, await service.SendDueAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, await service.SendDueAsync(TestContext.Current.CancellationToken));

        var message = Assert.Single(_sent);
        Assert.Equal("jordan@example.test", message.To);
        Assert.Contains("Jordan Guest, you left something behind.", message.TextBody);
        Assert.Contains("https://store.test/orders/resume/", message.TextBody);
        Assert.Equal(_now, order.RecoveryEmailSentUtc);
    }

    [Fact]
    public async Task TheLinkNamesTheOrder()
    {
        LeftOrder("order-1", "session-1");
        AddSession("session-1", "Jordan Guest", "jordan@example.test");

        await CreateService().SendDueAsync(TestContext.Current.CancellationToken);

        var url = Assert.Single(_sent).TextBody.Split('\n').Select(line => line.Trim()).First(line => line.Contains("/orders/resume/", StringComparison.Ordinal));
        var token = url[(url.LastIndexOf('/') + 1)..];

        Assert.Equal("order-1", CreateLinks().Read(Uri.UnescapeDataString(token)));
        Assert.Null(CreateLinks().Read(token + "x"));
    }

    [Fact]
    public async Task SkipsABuyerWhoPaidSince()
    {
        LeftOrder("order-1", "session-1");
        AddSession("session-1", "Jordan Guest", "jordan@example.test");
        _orders.Orders.Add(new Order
        {
            ItemId = "order-2",
            Status = OrderStatus.Paid,
            CustomerEmail = "Jordan@Example.test",
            CreatedUtc = _now.AddHours(-2),
            Lines = [new OrderLine { Title = "Contoso Cap", Quantity = 1, LineTotal = 25m }],
        });

        Assert.Equal(0, await CreateService().SendDueAsync(TestContext.Current.CancellationToken));
        Assert.Empty(_sent);
    }

    [Theory]
    [InlineData(OrderCancelReason.CanceledByStore)]
    [InlineData(OrderCancelReason.Replaced)]
    [InlineData(OrderCancelReason.Unavailable)]
    public async Task SkipsAnOrderTheBuyerDidNotLeave(OrderCancelReason reason)
    {
        var order = LeftOrder("order-1", "session-1");
        order.CancelReason = reason;
        AddSession("session-1", "Jordan Guest", "jordan@example.test");

        Assert.Equal(0, await CreateService().SendDueAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WaitsForTheDelay()
    {
        var order = LeftOrder("order-1", "session-1");
        order.CanceledUtc = _now.AddMinutes(-30);
        AddSession("session-1", "Jordan Guest", "jordan@example.test");

        Assert.Equal(0, await CreateService().SendDueAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SendsNothingWhenTurnedOffOrWithoutABaseUrl()
    {
        LeftOrder("order-1", "session-1");
        AddSession("session-1", "Jordan Guest", "jordan@example.test");

        Assert.Equal(0, await CreateService(enabled: false).SendDueAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, await CreateService(baseUrl: null).SendDueAsync(TestContext.Current.CancellationToken));
        Assert.Empty(_sent);
    }

    [Fact]
    public async Task SkipsAnOrderWithNoWayToReachTheBuyer()
    {
        LeftOrder("order-1", "session-without-contact");

        Assert.Equal(0, await CreateService().SendDueAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void TheHtmlEncodesWhatTheBuyerAndStoreTyped()
    {
        var order = LeftOrder("order-1", "session-1");
        order.Lines[0].Title = "<script>alert(1)</script>";

        var (_, _, html) = CreateService().Build(order, "<b>Jordan</b>", "https://store.test/orders/resume/t\"x", "Contoso");

        Assert.DoesNotContain("<script>", html);
        Assert.DoesNotContain("<b>Jordan</b>", html);
        Assert.Contains("&lt;b&gt;Jordan&lt;/b&gt;", html);
        Assert.Contains("t&quot;x", html);
    }

    private Order LeftOrder(string orderId, string sessionId)
    {
        var order = new Order
        {
            ItemId = orderId,
            OrderNumber = "261018-" + orderId,
            Currency = "USD",
            Status = OrderStatus.Canceled,
            CancelReason = OrderCancelReason.CheckoutExpired,
            CanceledUtc = _now.AddHours(-3),
            CheckoutSessionId = sessionId,
            CreatedUtc = _now.AddHours(-5),
            ModifiedUtc = _now.AddHours(-3),
            Lines = [new OrderLine { Title = "Contoso Cap", Quantity = 2, UnitPrice = 25m, LineTotal = 50m }],
        };

        _orders.Orders.Add(order);

        return order;
    }

    private void AddSession(string sessionId, string name, string email)
    {
        var session = new CheckoutSession { SessionId = sessionId };
        session.Put(new CheckoutContactInfo { DisplayName = name, Email = email });

        _sessions[sessionId] = session;
    }

    private CheckoutRecoveryLinks CreateLinks()
        => new(_protection, new ResumeLinkGenerator(), SiteService(true, "https://store.test/").Object, Clock());

    private CheckoutRecoveryService CreateService(bool enabled = true, string baseUrl = "https://store.test/")
    {
        var sessions = new Mock<ICheckoutSessionStore>();
        sessions
            .Setup(store => store.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => _sessions.GetValueOrDefault(id));

        var siteService = SiteService(enabled, baseUrl);
        var links = new CheckoutRecoveryLinks(_protection, new ResumeLinkGenerator(), siteService.Object, Clock());
        var services = new ServiceCollection().AddSingleton(_email.Object).BuildServiceProvider();

        return new CheckoutRecoveryService(
            _orders,
            sessions.Object,
            links,
            siteService.Object,
            services,
            Clock(),
            NullLogger<CheckoutRecoveryService>.Instance,
            new PassThroughLocalizer<CheckoutRecoveryService>());
    }

    private static Mock<ISiteService> SiteService(bool enabled, string baseUrl)
    {
        var site = new Mock<ISite>();
        site.Setup(s => s.GetOrCreate<OrderSettings>()).Returns(new OrderSettings { SendRecoveryEmails = enabled, RecoveryDelayHours = 1 });
        site.SetupGet(s => s.BaseUrl).Returns(baseUrl);
        site.SetupGet(s => s.SiteName).Returns("Contoso");

        var siteService = new Mock<ISiteService>();
        siteService.Setup(s => s.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        return siteService;
    }

    private static IClock Clock()
        => Mock.Of<IClock>(clock => clock.UtcNow == _now);

    // Builds the resume path the way the route does: orders/resume/{token}.
    private sealed class ResumeLinkGenerator : LinkGenerator
    {
        public override string GetPathByAddress<TAddress>(HttpContext httpContext, TAddress address, RouteValueDictionary values, RouteValueDictionary ambientValues = null, PathString? pathBase = null, FragmentString fragment = default, LinkOptions options = null)
            => Path(values);

        public override string GetPathByAddress<TAddress>(TAddress address, RouteValueDictionary values, PathString pathBase = default, FragmentString fragment = default, LinkOptions options = null)
            => Path(values);

        public override string GetUriByAddress<TAddress>(HttpContext httpContext, TAddress address, RouteValueDictionary values, RouteValueDictionary ambientValues = null, string scheme = null, HostString? host = null, PathString? pathBase = null, FragmentString fragment = default, LinkOptions options = null)
            => throw new NotSupportedException();

        public override string GetUriByAddress<TAddress>(TAddress address, RouteValueDictionary values, string scheme, HostString host, PathString pathBase = default, FragmentString fragment = default, LinkOptions options = null)
            => throw new NotSupportedException();

        private static string Path(RouteValueDictionary values)
            => "/orders/resume/" + Uri.EscapeDataString(values["token"]?.ToString() ?? string.Empty);
    }

    private sealed class PassThroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
