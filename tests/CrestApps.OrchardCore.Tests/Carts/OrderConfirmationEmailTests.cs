using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Handlers;
using CrestApps.OrchardCore.Orders.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Email;
using OrchardCore.Infrastructure;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Carts;

/// <summary>
/// A paid order's confirmation is emailed to the customer once, saying what they bought, what it came to and where
/// it is billed.
/// </summary>
public sealed class OrderConfirmationEmailTests
{
    private static readonly DateTime _now = new(2026, 10, 17, 9, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IEmailService> _email = new();
    private readonly Mock<IOrderStore> _orders = new();

    private static Order PaidOrder() => new()
    {
        ItemId = "order-1",
        OrderNumber = "261017-ABC123",
        Currency = "USD",
        Status = OrderStatus.Paid,
        CustomerName = "Jordan Guest",
        CustomerEmail = "jordan@example.test",
        Subtotal = 50m,
        Total = 54m,
        TaxAmount = 4m,
        Lines =
        [
            new OrderLine { Title = "Contoso Cap", PriceName = "S / Matte", Quantity = 2, UnitPrice = 25m, LineTotal = 50m },
        ],
        BillingAddress = new Address { Name = "Jordan Guest", AddressLine1 = "1 Infinite Loop", City = "Cupertino", Region = "CA", PostalCode = "95014", Country = "US" },
    };

    private static OrderConfirmationEmailBuilder CreateBuilder()
        => new(new ServiceCollection().BuildServiceProvider(), new PassThroughLocalizer<OrderConfirmationEmailBuilder>());

    private OrderConfirmationCheckoutHandler CreateHandler(Order order, bool send = true, bool withEmailService = true)
    {
        _orders.Setup(orders => orders.FindByIdAsync(order.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var site = new Mock<ISite>();
        site.Setup(s => s.GetOrCreate<OrderSettings>()).Returns(new OrderSettings { SendConfirmations = send });

        var siteService = new Mock<ISiteService>();
        siteService.Setup(s => s.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        var services = new ServiceCollection();

        if (withEmailService)
        {
            services.AddSingleton(_email.Object);
        }

        var clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(_now);

        var links = new Mock<IOrderLinkService>();
        links.Setup(l => l.CreateUrlAsync(It.IsAny<Order>())).ReturnsAsync("https://store.test/orders/view/token");

        var sender = new OrderConfirmationSender(_orders.Object, CreateBuilder(), links.Object, services.BuildServiceProvider(), clock.Object, NullLogger<OrderConfirmationSender>.Instance, new AsBuiltOrderEmailTemplates());

        return new OrderConfirmationCheckoutHandler(_orders.Object, siteService.Object, sender);
    }

    private static CheckoutFlowCompletedContext Completed(Order order)
        => new(new CheckoutFlow(new CheckoutSession { SessionId = "session-1", ReferenceType = CheckoutReferenceTypes.Order, ReferenceId = order.ItemId }));

    [Fact]
    public async Task TheEmail_SaysWhatWasBought_WhatItCameTo_AndWhereItIsBilled()
    {
        var email = await CreateBuilder().BuildAsync(PaidOrder());

        Assert.Contains("261017-ABC123", email.Subject);
        Assert.Contains("Thank you for your order, Jordan Guest.", email.Text);
        Assert.Contains("2 × Contoso Cap (S / Matte)", email.Text);
        Assert.Contains("1 Infinite Loop", email.Text);
        Assert.Contains("Cupertino CA 95014", email.Html);
    }

    [Fact]
    public async Task TheEmail_ShowsTheDiscounts()
    {
        var order = PaidOrder();
        order.Discounts = [new OrderDiscount { Code = "SAVE10", Description = "10% off", Amount = 5m }];

        var email = await CreateBuilder().BuildAsync(order);

        Assert.Contains("Discount (SAVE10)", email.Text);
        Assert.Matches(@"Discount \(SAVE10\)\s+-\D*5", email.Text);
    }

    [Fact]
    public async Task TheEmail_SaysHowAndWhereTheOrderShips()
    {
        var order = PaidOrder();
        order.Shipping = new OrderShipping { Name = "Express", Amount = 15m };
        order.ShippingAddress = new Address { Name = "Sam Lee", AddressLine1 = "400 Pine St", City = "Seattle", Region = "WA", PostalCode = "98101", Country = "US" };

        var email = await CreateBuilder().BuildAsync(order);

        Assert.Contains("Shipping (Express)", email.Text);
        Assert.Contains("Ships to:", email.Text);
        Assert.Contains("400 Pine St", email.Text);
        Assert.Contains("Seattle WA 98101", email.Html);

        order.Shipping.Amount = 0;

        Assert.Contains("Free", (await CreateBuilder().BuildAsync(order)).Text);
    }

    [Fact]
    public async Task WhatTheOrderSays_IsEncodedInTheHtml()
    {
        var order = PaidOrder();
        order.Lines[0].Title = "<script>alert(1)</script>";

        var email = await CreateBuilder().BuildAsync(order);

        Assert.DoesNotContain("<script>", email.Html);
        Assert.Contains("&lt;script&gt;", email.Html);
    }

    [Fact]
    public async Task APaidOrder_IsConfirmedOnce()
    {
        var order = PaidOrder();
        _email.Setup(email => email.SendAsync(It.IsAny<MailMessage>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());

        var handler = CreateHandler(order);

        await handler.CompletedAsync(Completed(order));
        await handler.CompletedAsync(Completed(order));

        // A guest's confirmation links to the order.
        _email.Verify(email => email.SendAsync(It.Is<MailMessage>(message => message.To == "jordan@example.test" && message.TextBody.Contains("https://store.test/orders/view/token")), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(_now, order.ConfirmationSentUtc);
    }

    [Fact]
    public async Task AnEmailThatFails_LeavesTheOrderUnconfirmed()
    {
        var order = PaidOrder();
        _email.Setup(email => email.SendAsync(It.IsAny<MailMessage>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Failed());

        await CreateHandler(order).CompletedAsync(Completed(order));

        Assert.Null(order.ConfirmationSentUtc);
    }

    [Fact]
    public async Task NothingIsSent_WhenTurnedOff_WithoutAnEmailService_OrForAnUnpaidOrder()
    {
        var order = PaidOrder();

        await CreateHandler(order, send: false).CompletedAsync(Completed(order));
        await CreateHandler(order, withEmailService: false).CompletedAsync(Completed(order));

        order.Status = OrderStatus.AwaitingPayment;
        await CreateHandler(order).CompletedAsync(Completed(order));

        _email.Verify(email => email.SendAsync(It.IsAny<MailMessage>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Null(order.ConfirmationSentUtc);
    }

    private sealed class PassThroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
