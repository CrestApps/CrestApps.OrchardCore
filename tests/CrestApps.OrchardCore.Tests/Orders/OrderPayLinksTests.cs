using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Services;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Orders;

/// <summary>
/// Every page that offers to pay an order asks the same question: whether the order can be paid, and from where.
/// </summary>
public sealed class OrderPayLinksTests
{
    private const string OrderPayUrl = "/orders/view/token/pay";

    private static readonly DateTime _now = new(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(OrderStatus.Draft, true)]
    [InlineData(OrderStatus.AwaitingPayment, true)]
    [InlineData(OrderStatus.Paid, false)]
    [InlineData(OrderStatus.Canceled, false)]
    public void AnInvoicedOrder_IsPaidByLink_UntilItIsPaidOrCanceled(OrderStatus status, bool expected)
    {
        // Arrange
        var order = new Order { Status = status, Invoice = new OrderInvoice() };

        // Act
        var canPay = Create().CanPayByLink(order);

        // Assert
        Assert.Equal(expected, canPay);
    }

    [Fact]
    public void AnOrderWithoutAnInvoice_OrWithAnExpiredQuote_IsNotPaidByLink()
    {
        // Arrange
        var links = Create();
        var expired = new Order
        {
            Status = OrderStatus.Draft,
            Invoice = new OrderInvoice(),
            Quote = new OrderQuote { Status = OrderQuoteStatus.Sent, ExpiresUtc = _now.AddDays(-1) },
        };

        // Act
        var withoutInvoice = links.CanPayByLink(new Order { Status = OrderStatus.Draft });
        var withExpiredQuote = links.CanPayByLink(expired);

        // Assert
        Assert.False(withoutInvoice);
        Assert.False(withExpiredQuote);
    }

    [Fact]
    public async Task AnInvoicedOrder_IsPaidFromItsOwnPayPage()
    {
        // Arrange
        var order = new Order { ItemId = "order-1", Status = OrderStatus.AwaitingPayment, Invoice = new OrderInvoice() };

        // Act
        var url = await Create().GetPayUrlAsync(order, OrderPayUrl);

        // Assert
        Assert.Equal(OrderPayUrl, url);
    }

    [Fact]
    public async Task ACanceledOrder_OrOneOwingNothing_HasNoPayLink()
    {
        // Arrange
        var links = Create();
        var canceled = new Order { ItemId = "order-1", Status = OrderStatus.Canceled, Invoice = new OrderInvoice() };
        var placed = new Order { ItemId = "order-2", Status = OrderStatus.Paid };

        // Act
        var canceledUrl = await links.GetPayUrlAsync(canceled, OrderPayUrl);
        var placedUrl = await links.GetPayUrlAsync(placed, OrderPayUrl);

        // Assert
        Assert.Null(canceledUrl);
        Assert.Null(placedUrl);
    }

    private static OrderPayLinks Create()
        => new(new ServiceCollection().BuildServiceProvider(), new TestClock(_now));
}
