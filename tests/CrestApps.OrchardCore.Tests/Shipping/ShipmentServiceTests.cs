using System.Text.Encodings.Web;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Shipping.Handlers;
using CrestApps.OrchardCore.Shipping.Models;
using CrestApps.OrchardCore.Shipping.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Email;
using OrchardCore.Infrastructure;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Shipping;

/// <summary>
/// An order is shipped in one or several parts, each with its carrier and tracking link, and knows how much of it has
/// left.
/// </summary>
public sealed class ShipmentServiceTests
{
    private static readonly DateTime _now = new(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);

    private readonly FakeShipmentStore _shipments = new();
    private readonly List<string> _events = [];
    private readonly Order _order = new()
    {
        ItemId = "order-1",
        OrderNumber = "261017-SHIP01",
        Status = OrderStatus.Paid,
        CustomerName = "Sam Lee",
        CustomerEmail = "sam@example.com",
        Lines =
        [
            Line("cap", "Contoso Cap", 3, ProductType.Good),
            Line("mug", "Mug", 1, ProductType.Good),
            Line("ebook", "Guide", 1, ProductType.Digital),
        ],
    };

    [Fact]
    public async Task AnOrderShippedInTwoParts_ShowsBoth_EachWithItsTrackingLink()
    {
        var service = CreateService();

        var first = await service.CreateAsync(_order, [new("cap", 2)], new ShipmentDetails("UPS", "1Z999"), ship: true, TestContext.Current.CancellationToken);

        Assert.True(first.Succeeded);
        Assert.Equal(OrderFulfillmentStatus.PartiallyFulfilled, _order.FulfillmentStatus);
        Assert.Equal(new Dictionary<string, int> { ["cap"] = 1, ["mug"] = 1 }, await service.GetRemainingAsync(_order, TestContext.Current.CancellationToken));

        var second = await service.CreateAsync(_order, [new("cap", 1), new("mug", 1)], new ShipmentDetails("DHL", "JD0042", "https://track.example.com/JD0042"), ship: true, TestContext.Current.CancellationToken);

        Assert.True(second.Succeeded);
        Assert.Equal(OrderFulfillmentStatus.Fulfilled, _order.FulfillmentStatus);
        Assert.Empty(await service.GetRemainingAsync(_order, TestContext.Current.CancellationToken));

        var shipments = await _shipments.ListByOrderAsync("order-1", TestContext.Current.CancellationToken);

        // The carrier's link from the settings, with the number in it; a link given is kept.
        Assert.Equal([(1, "https://www.ups.com/track?tracknum=1Z999"), (2, "https://track.example.com/JD0042")], shipments.Select(shipment => (shipment.Number, shipment.TrackingUrl)));
        Assert.All(shipments, shipment => Assert.Equal((ShipmentStatus.Shipped, _now), (shipment.Status, shipment.ShippedUtc.Value)));
        Assert.Equal(["Created:1", "Shipped:1", "Created:2", "Shipped:2"], _events);
    }

    [Fact]
    public async Task APendingShipment_SetsItemsAside_ButTheOrderIsntShippedUntilItLeaves()
    {
        var service = CreateService();

        var packing = (await service.CreateAsync(_order, [new("cap", 3), new("mug", 1)], new ShipmentDetails(null, null), ship: false, TestContext.Current.CancellationToken)).Shipment;

        Assert.Equal(ShipmentStatus.Pending, packing.Status);
        Assert.Equal(OrderFulfillmentStatus.Unfulfilled, _order.FulfillmentStatus);
        Assert.Empty(await service.GetRemainingAsync(_order, TestContext.Current.CancellationToken));

        // Delivered only follows shipped.
        Assert.Equal(ShipmentError.InvalidStatus, (await service.DeliverAsync(packing, TestContext.Current.CancellationToken)).Error);

        Assert.True((await service.ShipAsync(packing, new ShipmentDetails("UPS", "1Z111"), TestContext.Current.CancellationToken)).Succeeded);
        Assert.Equal(OrderFulfillmentStatus.Fulfilled, _order.FulfillmentStatus);
        Assert.Equal("https://www.ups.com/track?tracknum=1Z111", packing.TrackingUrl);

        Assert.True((await service.DeliverAsync(packing, TestContext.Current.CancellationToken)).Succeeded);
        Assert.Equal((ShipmentStatus.Delivered, _now), (packing.Status, packing.DeliveredUtc.Value));
        Assert.Equal(OrderFulfillmentStatus.Fulfilled, _order.FulfillmentStatus);
    }

    [Fact]
    public async Task RemovingAPendingShipment_OrAReturn_PutsItsItemsBack()
    {
        var service = CreateService();

        var packing = (await service.CreateAsync(_order, [new("mug", 1)], new ShipmentDetails(null, null), ship: false, TestContext.Current.CancellationToken)).Shipment;

        Assert.True((await service.DeleteAsync(packing, TestContext.Current.CancellationToken)).Succeeded);
        Assert.Equal(1, (await service.GetRemainingAsync(_order, TestContext.Current.CancellationToken))["mug"]);

        var sent = (await service.CreateAsync(_order, [new("cap", 3), new("mug", 1)], new ShipmentDetails("UPS", "1Z222"), ship: true, TestContext.Current.CancellationToken)).Shipment;

        // A shipment that left can't be removed, only returned.
        Assert.Equal(ShipmentError.InvalidStatus, (await service.DeleteAsync(sent, TestContext.Current.CancellationToken)).Error);
        Assert.True((await service.ReturnAsync(sent, TestContext.Current.CancellationToken)).Succeeded);
        Assert.Equal(OrderFulfillmentStatus.Unfulfilled, _order.FulfillmentStatus);
        Assert.Equal(new Dictionary<string, int> { ["cap"] = 3, ["mug"] = 1 }, await service.GetRemainingAsync(_order, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task OnlyAPaidOrdersGoods_AreShipped_AndNeverMoreThanIsLeft()
    {
        var service = CreateService();

        Assert.Equal(ShipmentError.TooMany, (await service.CreateAsync(_order, [new("cap", 4)], null, ship: true, TestContext.Current.CancellationToken)).Error);
        Assert.Equal(ShipmentError.TooMany, (await service.CreateAsync(_order, [new("ebook", 1)], null, ship: true, TestContext.Current.CancellationToken)).Error);
        Assert.Equal(ShipmentError.NothingToShip, (await service.CreateAsync(_order, [new("cap", 0)], null, ship: true, TestContext.Current.CancellationToken)).Error);

        _order.Status = OrderStatus.AwaitingPayment;

        Assert.Equal(ShipmentError.OrderNotPaid, (await service.CreateAsync(_order, [new("cap", 1)], null, ship: true, TestContext.Current.CancellationToken)).Error);
        Assert.Empty(await _shipments.ListByOrderAsync("order-1", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task OnlyWebTrackingLinks_AreKept()
    {
        var service = CreateService();

        var shipment = (await service.CreateAsync(_order, [new("mug", 1)], new ShipmentDetails("Courier", "C-1", "javascript:alert(1)"), ship: true, TestContext.Current.CancellationToken)).Shipment;

        Assert.Null(shipment.TrackingUrl);
    }

    [Fact]
    public void AnOrderWithoutGoods_HasNoFulfillmentStatus()
    {
        var order = new Order { Lines = [Line("ebook", "Guide", 1, ProductType.Digital)] };

        Assert.Null(DefaultShipmentService.GetFulfillmentStatus(order, []));
        Assert.Empty(DefaultShipmentService.GetRemaining(order, []));
    }

    [Fact]
    public void AnOrderPickedUp_IsEmailedAsReadyForPickup_WithWhereAndWhatToKnow()
    {
        var order = new Order
        {
            OrderNumber = "261018-PICK01",
            CustomerName = "Sam",
            ShippingAddress = new Address { Name = "Downtown store", AddressLine1 = "1 Main St", City = "Seattle", Country = "US" },
            Shipping = new OrderShipping { ProviderName = OrderShipping.PickupProviderName, Name = "Pick up at Downtown store", Description = "Bring your order number." },
        };
        var shipment = new Shipment { Lines = [new ShipmentLine { LineId = "cap", Title = "Contoso Cap", Quantity = 1 }] };

        var email = new ShipmentEmailBuilder(HtmlEncoder.Default, new PassThroughStringLocalizer<ShipmentEmailBuilder>()).Build(order, shipment);

        Assert.True(order.Shipping.IsPickup);
        Assert.Equal("Your order 261018-PICK01 is ready for pickup", email.Subject);
        Assert.Contains("Collect it from Downtown store, 1 Main St, Seattle.", email.Text);
        Assert.Contains("Bring your order number.", email.Html);
        Assert.DoesNotContain("on its way", email.Text);
    }

    [Fact]
    public async Task TheBuyerIsEmailedOnce_WhenAShipmentLeaves_WithItsTrackingLink()
    {
        var sent = new List<MailMessage>();
        var email = new Mock<IEmailService>();
        email.Setup(service => service.SendAsync(It.IsAny<MailMessage>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<MailMessage, string, CancellationToken>((message, _, _) => sent.Add(message))
            .ReturnsAsync(Result.Success());

        var services = new ServiceCollection().AddSingleton(email.Object).BuildServiceProvider();
        var links = new Mock<IOrderLinkService>();
        links.Setup(link => link.CreateUrlAsync(It.IsAny<Order>())).ReturnsAsync("https://store.example.com/orders/view/token");

        var handler = new ShippedEmailShipmentHandler(
            SiteService(),
            new ShipmentEmailBuilder(HtmlEncoder.Default, new PassThroughStringLocalizer<ShipmentEmailBuilder>()),
            links.Object,
            services,
            Clock(),
            NullLogger<ShippedEmailShipmentHandler>.Instance);

        var service = CreateService(handler);
        var shipment = (await service.CreateAsync(_order, [new("cap", 2)], new ShipmentDetails("UPS", "1Z333"), ship: true, TestContext.Current.CancellationToken)).Shipment;

        var message = Assert.Single(sent);
        Assert.Equal("sam@example.com", message.To);
        Assert.Contains("261017-SHIP01", message.Subject);
        Assert.Contains("2 × Contoso Cap", message.TextBody);
        Assert.Contains("https://www.ups.com/track?tracknum=1Z333", message.TextBody);
        Assert.Contains("https://store.example.com/orders/view/token", message.HtmlBody);
        Assert.Equal(_now, shipment.ShippedEmailSentUtc);

        // Delivered tells the buyer nothing more by email.
        await service.DeliverAsync(shipment, TestContext.Current.CancellationToken);

        Assert.Single(sent);
    }

    private DefaultShipmentService CreateService(params IShipmentEventHandler[] extra)
    {
        var orders = new Mock<IOrderStore>();
        orders.Setup(store => store.FindByIdAsync("order-1", It.IsAny<CancellationToken>())).ReturnsAsync(_order);

        return new DefaultShipmentService(_shipments, orders.Object, SiteService(), [new RecordingHandler(_events), .. extra], Clock());
    }

    private static ISiteService SiteService()
    {
        var site = new Mock<ISite>();
        site.Setup(value => value.GetOrCreate<ShippingSettings>()).Returns(new ShippingSettings
        {
            Carriers = [new ShippingCarrier { Name = "UPS", TrackingUrlTemplate = "https://www.ups.com/track?tracknum={0}" }],
        });

        var siteService = new Mock<ISiteService>();
        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        return siteService.Object;
    }

    private static IClock Clock()
    {
        var clock = new Mock<IClock>();
        clock.Setup(value => value.UtcNow).Returns(_now);

        return clock.Object;
    }

    private static OrderLine Line(string id, string title, int quantity, ProductType type)
        => new() { LineId = id, ContentItemId = id, Title = title, Quantity = quantity, UnitPrice = 10m, LineTotal = quantity * 10m, ProductType = type };

    private sealed class RecordingHandler : IShipmentEventHandler
    {
        private readonly List<string> _events;

        public RecordingHandler(List<string> events)
        {
            _events = events;
        }

        public Task CreatedAsync(ShipmentContext context)
            => Record("Created", context);

        public Task ShippedAsync(ShipmentContext context)
            => Record("Shipped", context);

        public Task DeliveredAsync(ShipmentContext context)
            => Record("Delivered", context);

        public Task ReturnedAsync(ShipmentContext context)
            => Record("Returned", context);

        private Task Record(string name, ShipmentContext context)
        {
            _events.Add($"{name}:{context.Shipment.Number}");

            return Task.CompletedTask;
        }
    }

    private sealed class FakeShipmentStore : IShipmentStore
    {
        private readonly List<Shipment> _shipments = [];

        public ValueTask<Shipment> FindByIdAsync(string shipmentId, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(_shipments.FirstOrDefault(shipment => shipment.ItemId == shipmentId));

        public Task<Shipment> FindByTrackingNumberAsync(string trackingNumber, CancellationToken cancellationToken = default)
            => Task.FromResult(_shipments.FirstOrDefault(shipment => shipment.TrackingNumber == trackingNumber));

        public Task<IReadOnlyList<Shipment>> ListByOrderAsync(string orderId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Shipment>>([.. _shipments.Where(shipment => shipment.OrderId == orderId).OrderBy(shipment => shipment.Number)]);

        public ValueTask CreateAsync(Shipment shipment, CancellationToken cancellationToken = default)
        {
            _shipments.Add(shipment);

            return ValueTask.CompletedTask;
        }

        public ValueTask UpdateAsync(Shipment shipment, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask<bool> DeleteAsync(Shipment shipment, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(_shipments.Remove(shipment));
    }
}
