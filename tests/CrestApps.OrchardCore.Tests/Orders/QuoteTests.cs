using System.Text.Encodings.Web;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Orders;

/// <summary>
/// A buyer asks for a quote from their cart; staff price it and send it; the buyer accepts it by paying before it
/// expires, or turns it down.
/// </summary>
public sealed class QuoteTests
{
    private static readonly DateTime _now = new(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void AQuote_IsPaidOnlyOnceSent_AndUntilItExpires()
    {
        Assert.True(QuoteService.CanPay(new Order(), _now));
        Assert.False(QuoteService.CanPay(Quote(OrderQuoteStatus.Requested), _now));
        Assert.True(QuoteService.CanPay(Quote(OrderQuoteStatus.Sent), _now));
        Assert.True(QuoteService.CanPay(Quote(OrderQuoteStatus.Sent, expires: _now.AddDays(1)), _now));
        Assert.False(QuoteService.CanPay(Quote(OrderQuoteStatus.Sent, expires: _now.AddMinutes(-1)), _now));
        Assert.False(QuoteService.CanPay(Quote(OrderQuoteStatus.Declined), _now));
    }

    [Fact]
    public void AQuote_IsTurnedDown_OnlyBeforeTheBuyerStartsPayingIt()
    {
        Assert.True(QuoteService.CanDecline(Quote(OrderQuoteStatus.Requested)));
        Assert.True(QuoteService.CanDecline(Quote(OrderQuoteStatus.Sent)));
        Assert.False(QuoteService.CanDecline(Quote(OrderQuoteStatus.Sent, status: OrderStatus.AwaitingPayment)));
        Assert.False(QuoteService.CanDecline(Quote(OrderQuoteStatus.Declined)));
        Assert.False(QuoteService.CanDecline(new Order { Status = OrderStatus.Draft }));
    }

    [Fact]
    public async Task TurningAQuoteDown_CancelsItsOrder_AsDeclined()
    {
        var orders = new Mock<IOrderService>();
        orders.Setup(service => service.CancelAsync(It.IsAny<Order>(), OrderCancelReason.Declined, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        var quotes = new QuoteService(null, orders.Object, null, null, null, null, null, clock.Object, new PassThroughStringLocalizer<QuoteService>());
        var order = Quote(OrderQuoteStatus.Sent);

        Assert.True(await quotes.DeclineAsync(order, "buyer", TestContext.Current.CancellationToken));
        Assert.Equal(OrderQuoteStatus.Declined, order.Quote.Status);
        Assert.Equal(_now, order.Quote.RespondedUtc);

        // Once turned down, it stays that way.
        Assert.False(await quotes.DeclineAsync(order, "buyer", TestContext.Current.CancellationToken));
        orders.Verify(service => service.CancelAsync(order, OrderCancelReason.Declined, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ThePricesStaffSet_ReplaceTheCatalogs_OnTheirLinesOnly()
    {
        var order = new Order
        {
            Lines =
            [
                new() { LineId = "hat", Quantity = 3, UnitPrice = 30m, LineTotal = 90m },
                new() { LineId = "cap", Quantity = 2, UnitPrice = 10m, LineTotal = 20m },
            ],
        };

        DraftOrderService.ApplyPrices(order, [
            new DraftOrderLineInput("hat", "hat-item", null, 3, 24.505m),
            new DraftOrderLineInput("cap", "cap-item", null, 2),
        ]);

        Assert.Equal(24.51m, order.Lines[0].QuotedUnitPrice);
        Assert.Equal(24.51m, order.Lines[0].UnitPrice);
        Assert.Equal(73.53m, order.Lines[0].LineTotal);
        Assert.Null(order.Lines[1].QuotedUnitPrice);
        Assert.Equal(20m, order.Lines[1].LineTotal);
        Assert.Equal(93.53m, order.Subtotal);
    }

    [Fact]
    public void AQuotesEmail_SaysItsAQuote_AndUntilWhenItsOffered()
    {
        var drafts = new DraftOrderService(null, null, null, null, null, null, HtmlEncoder.Default, null, NullLogger<DraftOrderService>.Instance, new PassThroughStringLocalizer<DraftOrderService>());
        var order = Quote(OrderQuoteStatus.Requested);

        order.OrderNumber = "261020-QUOTE1";
        order.CustomerName = "Sam";
        order.Currency = "USD";
        order.Lines = [new() { LineId = "hat", Title = "Hat", Quantity = 3, UnitPrice = 25m, LineTotal = 75m }];
        order.Subtotal = 75m;
        order.Invoice = new OrderInvoice { DueUtc = new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc) };

        var (subject, text, html) = drafts.BuildInvoiceEmail(order, "https://store.example.com/orders/view/token/pay");

        Assert.Equal("Your quote 261020-QUOTE1 is ready", subject);
        Assert.Contains("Hi Sam, here's the quote you asked for.", text);
        Assert.Contains("The quote is offered until", text);
        Assert.DoesNotContain("Please pay by", text);
        Assert.Contains("Review and accept", html);
    }

    [Fact]
    public void ALineAtAnAgreedPrice_TakesNoDiscount()
    {
        var invoice = new CheckoutInvoice
        {
            Currency = "USD",
            LineItems =
            [
                new CheckoutLineItem { ItemId = "quoted", Quantity = 3, UnitPrice = 25m, ExcludeFromDiscounts = true },
                new CheckoutLineItem { ItemId = "catalog", Quantity = 1, UnitPrice = 10m },
            ],
        };

        Assert.False(CheckoutDiscountBase.IsDiscountable(invoice.LineItems[0], DiscountTarget.OneTime));
        Assert.True(CheckoutDiscountBase.IsDiscountable(invoice.LineItems[1], DiscountTarget.OneTime));
        Assert.Equal(10m, CheckoutDiscountBase.GetAmount(invoice, DiscountTarget.OneTime));
    }

    private static Order Quote(OrderQuoteStatus quoteStatus, DateTime? expires = null, OrderStatus status = OrderStatus.Draft)
        => new()
        {
            Status = status,
            Invoice = new OrderInvoice(),
            Quote = new OrderQuote { RequestedUtc = _now.AddDays(-2), Status = quoteStatus, ExpiresUtc = expires },
        };
}
