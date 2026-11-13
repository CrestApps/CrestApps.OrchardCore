using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.Commerce.Emails;
using CrestApps.OrchardCore.Orders;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Controllers;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Services;
using CrestApps.OrchardCore.Orders.ViewModels;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Throttling;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.DisplayManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Orders;

/// <summary>
/// SEC-C17f: an order number and an e-mail address no longer open the order. The order's link is mailed to the address
/// it was placed with, the answer is the same whether or not they matched, and guesses are limited per e-mail address.
/// </summary>
public sealed class OrderTrackLookupTests
{
    private readonly Order _order = new() { ItemId = "order-1", OrderNumber = "261018-ABC123", Status = OrderStatus.Paid, CustomerEmail = "pat@example.test" };
    private readonly Mock<ICommerceEmailSender> _emails = new();
    private readonly List<string> _limited = [];

    [Fact]
    public async Task Track_AMatchingNumberAndEmail_MailsTheLinkInsteadOfOpeningTheOrder()
    {
        // Arrange
        var controller = CreateController();

        // Act
        var result = await controller.Track(new TrackOrderViewModel { OrderNumber = "#261018-abc123", Email = "pat@example.test" });

        // Assert
        Assert.IsNotType<RedirectResult>(result);
        _emails.Verify(sender => sender.SendAsync(It.Is<CommerceEmail>(email => email.To == "pat@example.test"), It.IsAny<Func<CancellationToken, Task<CommerceEmailContent>>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Track_LimitsGuessesPerEmailAddress()
    {
        // Arrange
        var controller = CreateController();

        // Act
        await controller.Track(new TrackOrderViewModel { OrderNumber = "261018-XYZ", Email = "pat@example.test" });

        // Assert
        Assert.Contains(OrderConstants.AttemptPolicies.OrderLookupEmail, _limited);
    }

    [Fact]
    public async Task Track_OnAStoreThatCantSendEmail_SaysSoInsteadOfClaimingALinkWasSent()
    {
        // Arrange
        var controller = CreateController(canSend: false);

        // Act
        var result = await controller.Track(new TrackOrderViewModel { OrderNumber = "261018-ABC123", Email = "pat@example.test" });

        // Assert
        var model = Assert.IsAssignableFrom<FindOrderViewModel>(Assert.IsType<ViewResult>(result).Model);

        Assert.True(model.EmailUnavailable);
        _emails.Verify(sender => sender.SendAsync(It.IsAny<CommerceEmail>(), It.IsAny<Func<CancellationToken, Task<CommerceEmailContent>>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private OrderViewController CreateController(bool canSend = true)
    {
        var orders = new Mock<IOrderStore>();
        orders.Setup(store => store.FindByNumberAsync("261018-ABC123", It.IsAny<CancellationToken>())).ReturnsAsync(_order);
        orders.Setup(store => store.PageAsync(It.IsAny<OrderQuery>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((1, (IReadOnlyList<Order>)[_order]));

        var links = new Mock<IOrderLinkService>();
        links.Setup(service => service.CreateUrlAsync(It.IsAny<Order>())).ReturnsAsync("https://shop.example/orders/view/token");

        var limiter = new Mock<IAttemptLimiter>();
        limiter.Setup(value => value.AcquireAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string policy, string _, CancellationToken _) => _limited.Add(policy))
            .ReturnsAsync(AttemptLimitResult.Unlimited);

        _emails.SetupGet(sender => sender.CanSend).Returns(canSend);
        _emails.Setup(sender => sender.SendAsync(It.IsAny<CommerceEmail>(), It.IsAny<Func<CancellationToken, Task<CommerceEmailContent>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var services = new ServiceCollection()
            .AddSingleton(orders.Object)
            .AddSingleton(links.Object)
            .AddSingleton((OrderLinksEmailBuilder)RuntimeHelpers.GetUninitializedObject(typeof(OrderLinksEmailBuilder)))
            .AddSingleton(limiter.Object)
            .AddSingleton(Mock.Of<IClock>(clock => clock.UtcNow == new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc)))
            .AddSingleton<IShapeFactory>(new ModelShapeFactory())
            .AddSingleton(_emails.Object)
            .AddSingleton(Mock.Of<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionaryFactory>(factory => factory.GetTempData(It.IsAny<HttpContext>()) == Mock.Of<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionary>()))
            .AddSingleton(typeof(IStringLocalizer<>), typeof(CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<>))
            .BuildServiceProvider();

        var controller = ActivatorUtilities.CreateInstance<OrderViewController>(services);

        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { RequestServices = services } };

        return controller;
    }
}
