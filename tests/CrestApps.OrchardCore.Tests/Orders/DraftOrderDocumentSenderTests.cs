using System.Text.Encodings.Web;
using CrestApps.OrchardCore.Commerce.Emails;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Orders;

/// <summary>
/// Sending a draft order goes to another module's document sender when one handles the order (an invoicing module), and
/// to the order's own invoice e-mail otherwise, as on a site without Invoicing. Either way the order is marked sent.
/// </summary>
public sealed class DraftOrderDocumentSenderTests
{
    private static readonly DateTime _now = new(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task WithoutADocumentSender_TheOrdersOwnInvoiceEmailIsSent()
    {
        // Arrange: no module sends order documents, as on a site without Invoicing.
        var email = new Mock<ICommerceEmailSender>();
        email.Setup(sender => sender.SendAsync(It.IsAny<CommerceEmail>(), It.IsAny<Func<CancellationToken, Task<CommerceEmailContent>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var order = NewDraftOrder();

        // Act
        var result = await CreateService(email.Object, new ServiceCollection().BuildServiceProvider()).SendAsync(order, "admin", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Sent, string.Join(" ", result.Errors));
        Assert.Equal(_now, order.Invoice.SentUtc);
        email.Verify(sender => sender.SendAsync(It.Is<CommerceEmail>(message => message.Kind == OrderEmailKinds.Invoice), It.IsAny<Func<CancellationToken, Task<CommerceEmailContent>>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WithADocumentSenderThatHandlesTheOrder_TheOrdersOwnEmailIsNotSent()
    {
        // Arrange: an invoicing module sends it as its invoice.
        var email = new Mock<ICommerceEmailSender>();
        var sender = new Mock<IOrderDocumentSender>();
        sender.Setup(documents => documents.SendAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>())).ReturnsAsync(new OrderDocumentSendResult { Note = "Sent as invoice INV-1001.", Message = "Sent as invoice INV-1001." });
        var order = NewDraftOrder();
        var services = new ServiceCollection().AddSingleton(sender.Object).BuildServiceProvider();

        // Act
        var result = await CreateService(email.Object, services).SendAsync(order, "admin", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Sent);
        Assert.Equal("Sent as invoice INV-1001.", result.Message);
        Assert.Equal(_now, order.Invoice.SentUtc);
        email.Verify(emails => emails.SendAsync(It.IsAny<CommerceEmail>(), It.IsAny<Func<CancellationToken, Task<CommerceEmailContent>>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static DraftOrderService CreateService(ICommerceEmailSender email, IServiceProvider services)
    {
        var store = new Mock<IOrderStore>();
        store.Setup(orders => orders.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Order)null);

        return new DraftOrderService(null, Mock.Of<IOrderService>(), store.Object, Mock.Of<IOrderLinkService>(), null, services, HtmlEncoder.Default, new StubClock(_now), new PassThroughStringLocalizer<DraftOrderService>(), email);
    }

    private static Order NewDraftOrder()
        => new()
        {
            ItemId = "order-1",
            OrderNumber = "1001",
            Currency = "USD",
            Status = OrderStatus.Draft,
            CustomerEmail = "pat@example.com",
            Invoice = new OrderInvoice { CreatedBy = "admin" },
        };
}
