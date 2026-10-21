using System.Text.Encodings.Web;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Services;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Moq;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Orders;

/// <summary>
/// A buyer can return the goods of a paid order within the window, less what earlier returns cover, and is told where
/// their return is by email.
/// </summary>
public sealed class OrderReturnTests
{
    private static readonly DateTime _paid = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private static readonly Order _order = new()
    {
        ItemId = "order-1",
        OrderNumber = "261001-ABCDEF",
        Status = OrderStatus.Paid,
        PaidUtc = _paid,
        Currency = "USD",
        Lines =
        [
            new() { LineId = "mug", Title = "Mug", Quantity = 3, UnitPrice = 10, LineTotal = 30, ProductType = ProductType.Good },
            new() { LineId = "ebook", Title = "E-book", Quantity = 1, UnitPrice = 5, LineTotal = 5, ProductType = ProductType.Digital },
            new() { LineId = "cap", Title = "Cap", Quantity = 1, UnitPrice = 20, LineTotal = 18, ProductType = ProductType.Good },
        ],
    };

    [Fact]
    public void Goods_CanBeReturned_LessWhatOpenReturnsCover_ButNotDeclinedOnes()
    {
        var returns = new[]
        {
            Return(OrderReturnStatus.Approved, ("mug", 2)),
            Return(OrderReturnStatus.Rejected, ("cap", 1)),
            Return(OrderReturnStatus.Canceled, ("mug", 1)),
        };

        var returnable = OrderReturnPolicy.GetReturnable(_order, returns);

        Assert.Equal(new Dictionary<string, int> { ["mug"] = 1, ["cap"] = 1 }, returnable);
    }

    [Theory]
    [InlineData(30, true)]
    [InlineData(31, false)]
    public void TheWindow_RunsFromPayment(int daysLater, bool open)
        => Assert.Equal(open, OrderReturnPolicy.IsOpen(_order, new ReturnSettings { WindowDays = 30 }, _paid.AddDays(daysLater)));

    [Fact]
    public void AUnitsPrice_IsWhatTheLinePaidPerUnit_DiscountsIncluded()
        => Assert.Equal(18m, OrderReturnPolicy.UnitPriceOf(_order.Lines[2]));

    [Fact]
    public void AnApprovalEmail_SaysHowToSendTheItemsBack_WithTheReturnNumber()
    {
        var sender = new OrderReturnEmailSender(
            Mock.Of<ICommerceEmailSender>(),
            Mock.Of<IOrderStore>(),
            Mock.Of<IOrderLinkService>(),
            Mock.Of<ISiteService>(),
            HtmlEncoder.Default,
            new PassThroughStringLocalizer<OrderReturnEmailSender>());

        var orderReturn = Return(OrderReturnStatus.Approved, ("mug", 2));
        orderReturn.StoreMessage = "Use the prepaid label.";

        var (subject, text, html) = sender.Build(orderReturn, OrderReturnEmailKind.Approved, "Send to: Returns, 1 Main St.", "https://store.example.com/orders/view/token");

        Assert.Equal("Your return R-261002-TEST is approved", subject);
        Assert.Contains("Send to: Returns, 1 Main St.", text);
        Assert.Contains("Use the prepaid label.", text);
        Assert.Contains("Put the return number R-261002-TEST in the parcel", text);
        Assert.Contains("2 × Mug", html);
        Assert.Contains("https://store.example.com/orders/view/token", html);
    }

    [Fact]
    public void ADraftOrdersInvoice_ListsItsLines_TheNote_AndTheLinkToPay()
    {
        var drafts = new DraftOrderService(null, null, null, null, null, null, HtmlEncoder.Default, null, new PassThroughStringLocalizer<DraftOrderService>(), null);
        var order = new Order
        {
            OrderNumber = "261019-DRAFT1",
            CustomerName = "Sam",
            Currency = "USD",
            Subtotal = 30,
            Lines = [new() { LineId = "mug", Title = "Mug", Quantity = 3, UnitPrice = 10, LineTotal = 30 }],
            Invoice = new OrderInvoice { Note = "As discussed on the phone.", DueUtc = new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc) },
        };

        var (subject, text, html) = drafts.BuildInvoiceEmail(order, "https://store.example.com/orders/view/token/pay");

        Assert.Equal("Your order 261019-DRAFT1 is ready to pay", subject);
        Assert.Contains("Hi Sam", text);
        Assert.Contains("As discussed on the phone.", text);
        Assert.Contains("3 × Mug", text);
        Assert.Contains("https://store.example.com/orders/view/token/pay", html);
    }

    private static OrderReturn Return(OrderReturnStatus status, params (string LineId, int Quantity)[] lines)
        => new()
        {
            Number = "R-261002-TEST",
            OrderId = _order.ItemId,
            OrderNumber = _order.OrderNumber,
            Status = status,
            Currency = "USD",
            Lines = [.. lines.Select(line => new OrderReturnLine { LineId = line.LineId, Title = line.LineId == "mug" ? "Mug" : "Cap", Quantity = line.Quantity })],
        };
}
