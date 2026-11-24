using System.Security.Claims;
using CrestApps.OrchardCore.Orders.Controllers;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Tests.Orders;

/// <summary>
/// The order page's Invoice button opens the invoice another module made for the order (Invoicing), a sent draft order's
/// included, and keeps today's behaviour without one.
/// </summary>
public sealed class OrderInvoiceLinkTests
{
    [Fact]
    public async Task ADraftOrderSentAsAnotherModulesInvoice_OpensThatInvoice()
    {
        // Arrange: a draft order Invoicing sent as INV-1002, still waiting for payment.
        var sender = new Mock<IOrderDocumentSender>();
        sender.Setup(documents => documents.GetInvoiceUrlAsync(It.IsAny<Order>(), true, It.IsAny<CancellationToken>())).ReturnsAsync("/Admin/invoices/Detail/inv-2");
        var controller = CreateController(new ServiceCollection().AddSingleton(sender.Object).BuildServiceProvider());

        // Act
        var result = await controller.Invoice("order-1", documents: null);

        // Assert
        Assert.Equal("/Admin/invoices/Detail/inv-2", Assert.IsType<RedirectResult>(result).Url);
    }

    [Fact]
    public async Task ADraftOrderWithoutAnotherModulesInvoice_IsNotFound_AsBefore()
    {
        // Arrange: no module invoices orders, as on a site without Invoicing.
        var controller = CreateController(new ServiceCollection().BuildServiceProvider());

        // Act
        var result = await controller.Invoice("order-1", documents: null);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    private static OrdersAdminController CreateController(IServiceProvider services)
    {
        var store = new Mock<IOrderStore>();
        store.Setup(orders => orders.FindByIdAsync("order-1", It.IsAny<CancellationToken>())).ReturnsAsync(new Order
        {
            ItemId = "order-1",
            OrderNumber = "1001",
            Currency = "USD",
            Status = OrderStatus.Draft,
            Invoice = new OrderInvoice { CreatedBy = "admin", SentUtc = DateTime.UtcNow },
        });

        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>())).ReturnsAsync(AuthorizationResult.Success());

        return new OrdersAdminController(
            store.Object,
            authorization.Object,
            Mock.Of<IShapeFactory>(),
            Mock.Of<INotifier>(),
            Options.Create(new PagerOptions()),
            Mock.Of<IStringLocalizer<OrdersAdminController>>(),
            Mock.Of<IHtmlLocalizer<OrdersAdminController>>(),
            new StubClock(DateTime.UtcNow))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    RequestServices = services,
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "admin")], "Test")),
                },
            },
        };
    }
}
