using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Orders.Controllers;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Orders;

/// <summary>
/// SEC-C17g: following an order's pay link again (a link scanner, a mail client's preview, a second click) reuses the
/// checkout it opened, so GET requests can't pile up checkout sessions.
/// </summary>
public sealed class OrderPayLinkSessionTests
{
    [Fact]
    public async Task Guest_OpenedTwice_StartsOneCheckout()
    {
        // Arrange
        var order = new Order { ItemId = "order-1", Status = OrderStatus.Draft, Invoice = new OrderInvoice(), Currency = "USD" };
        CheckoutSession started = null;
        var starts = 0;

        var engine = new Mock<ICheckoutEngine>();
        engine.Setup(value => value.StartAsync(It.IsAny<StartCheckoutRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                starts++;

                return started = new CheckoutSession { SessionId = "session-" + starts, Status = CheckoutSessionStatus.Pending, ReferenceType = CheckoutReferenceTypes.Order, ReferenceId = order.ItemId };
            });

        var sessions = new Mock<ICheckoutSessionStore>();
        sessions.Setup(value => value.GetByReferenceAsync(CheckoutReferenceTypes.Order, order.ItemId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => started);

        // The browser that opened it owns it, so the ownership-checked read finds it.
        sessions.Setup(value => value.GetAsync(It.IsAny<string>(), CheckoutSessionStatus.Pending, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string sessionId, CheckoutSessionStatus _, CancellationToken _) => started?.SessionId == sessionId ? started : null);

        var controller = CreateController(order, engine.Object, sessions.Object);

        // Act
        await controller.Guest("token");
        await controller.Guest("token");

        // Assert
        Assert.Equal(1, starts);
    }

    /// <summary>
    /// SEC-C17g (review): a link scanner, a mail preview or the buyer's other device opened the link first. The checkout
    /// it started belongs to that client, so sending the buyer to it gave them a 404 until it expired, a day later. The
    /// buyer gets a checkout of their own.
    /// </summary>
    [Fact]
    public async Task Guest_AfterAnotherClientOpenedTheLink_StartsTheBuyerTheirOwnCheckout()
    {
        // Arrange
        var order = new Order { ItemId = "order-1", Status = OrderStatus.Draft, Invoice = new OrderInvoice(), Currency = "USD" };
        var foreign = new CheckoutSession { SessionId = "scanner-session", Status = CheckoutSessionStatus.Pending, ReferenceType = CheckoutReferenceTypes.Order, ReferenceId = order.ItemId };
        var starts = 0;

        var engine = new Mock<ICheckoutEngine>();
        engine.Setup(value => value.StartAsync(It.IsAny<StartCheckoutRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new CheckoutSession { SessionId = "buyer-session-" + ++starts, Status = CheckoutSessionStatus.Pending });

        var sessions = new Mock<ICheckoutSessionStore>();
        sessions.Setup(value => value.GetByReferenceAsync(CheckoutReferenceTypes.Order, order.ItemId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(foreign);

        // This browser doesn't own the scanner's checkout: the ownership-checked read doesn't find it.
        sessions.Setup(value => value.GetAsync(It.IsAny<string>(), CheckoutSessionStatus.Pending, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CheckoutSession)null);

        var controller = CreateController(order, engine.Object, sessions.Object);

        // Act
        var result = await controller.Guest("token");

        // Assert
        Assert.Equal(1, starts);
        Assert.Equal("buyer-session-1", Assert.IsType<RedirectToRouteResult>(result).RouteValues["sessionId"]);
    }
    private static OrderPayController CreateController(Order order, ICheckoutEngine engine, ICheckoutSessionStore sessions)
    {
        var orders = new Mock<IOrderStore>();
        orders.Setup(store => store.FindByIdAsync(order.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var links = new Mock<IOrderLinkService>();
        links.Setup(service => service.GetOrderId("token")).Returns(order.ItemId);

        var services = new ServiceCollection()
            .AddSingleton(orders.Object)
            .AddSingleton(links.Object)
            .AddSingleton(engine)
            .AddSingleton(sessions)
            .AddSingleton(Mock.Of<IAuthorizationService>())
            .AddSingleton(Mock.Of<IClock>(clock => clock.UtcNow == new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc)))
            .AddSingleton<global::CrestApps.OrchardCore.Orders.Services.OrderPayLinks>()
            .AddSingleton(Mock.Of<global::CrestApps.OrchardCore.Throttling.IAttemptLimiter>(limiter => limiter.AcquireAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()) == Task.FromResult(global::CrestApps.OrchardCore.Throttling.AttemptLimitResult.Unlimited)))
            .BuildServiceProvider();

        var controller = ActivatorUtilities.CreateInstance<OrderPayController>(services);
        var url = new Mock<IUrlHelper>();

        url.Setup(helper => helper.RouteUrl(It.IsAny<UrlRouteContext>())).Returns("/orders/view/token");
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { RequestServices = services } };
        controller.Url = url.Object;

        return controller;
    }
}
