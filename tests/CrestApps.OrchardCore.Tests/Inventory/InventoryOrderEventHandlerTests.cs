using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.Inventory.Handlers;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Moq;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Inventory;

/// <summary>
/// An order holds its stock when it is made, sells it when it is paid, and lets it go when it is canceled; an order
/// that can't have its stock is refused with a reason the buyer understands.
/// </summary>
public sealed class InventoryOrderEventHandlerTests
{
    private static readonly DateTime _now = new(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IInventoryService> _inventory = new();

    private InventoryOrderEventHandler CreateHandler()
    {
        var clock = new Mock<IClock>();
        clock.Setup(value => value.UtcNow).Returns(_now);

        var site = new Mock<ISite>();
        site.Setup(value => value.GetOrCreate<InventorySettings>()).Returns(new InventorySettings { HoldMinutes = 30 });

        var siteService = new Mock<ISiteService>();
        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        return new InventoryOrderEventHandler(_inventory.Object, siteService.Object, clock.Object, new PassThroughStringLocalizer<InventoryOrderEventHandler>());
    }

    private static Order AnOrder()
        => new()
        {
            ItemId = "order-1",
            OrderNumber = "261017-AAA111",
            Lines = [new OrderLine { ContentItemId = "mug", VariantId = "blue", Quantity = 2 }],
        };

    [Fact]
    public async Task AMadeOrder_HoldsItsLinesForTheHoldTime()
    {
        _inventory
            .Setup(service => service.ReserveAsync("order-1", It.IsAny<IEnumerable<StockRequest>>(), _now.AddMinutes(30), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StockReservationResult());

        var context = new OrderCreatedContext(AnOrder());
        await CreateHandler().CreatedAsync(context, TestContext.Current.CancellationToken);

        Assert.Empty(context.Problems);
        _inventory.Verify(service => service.ReserveAsync("order-1", It.Is<IEnumerable<StockRequest>>(lines => lines.Single() == new StockRequest("mug", "blue", 2)), _now.AddMinutes(30), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task AShortOrder_IsRefused_SayingWhatIsLeft()
    {
        _inventory
            .Setup(service => service.ReserveAsync(It.IsAny<string>(), It.IsAny<IEnumerable<StockRequest>>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StockReservationResult
            {
                Shortages =
                [
                    new StockShortage(new StockItem { Title = "Mug (Blue)" }, 2, 1),
                    new StockShortage(new StockItem { Title = "Lamp" }, 1, 0),
                ],
            });

        var context = new OrderCreatedContext(AnOrder());
        await CreateHandler().CreatedAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal(["Only 1 of Mug (Blue) left. Lower the quantity to check out.", "Lamp is out of stock. Remove it to check out."], context.Problems);
    }

    [Fact]
    public async Task APaidOrder_SellsItsLines()
    {
        _inventory
            .Setup(service => service.CommitAsync(It.IsAny<string>(), It.IsAny<IEnumerable<StockRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StockReservationResult());

        await CreateHandler().PaidAsync(new OrderPaidContext(AnOrder()), TestContext.Current.CancellationToken);

        _inventory.Verify(service => service.CommitAsync("order-1", It.Is<IEnumerable<StockRequest>>(lines => lines.Single() == new StockRequest("mug", "blue", 2)), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task ACanceledOrder_LetsItsStockGo()
    {
        _inventory
            .Setup(service => service.ReleaseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StockReservationResult());

        await CreateHandler().CanceledAsync(new OrderCanceledContext(AnOrder(), OrderCancelReason.CheckoutExpired), TestContext.Current.CancellationToken);

        _inventory.Verify(service => service.ReleaseAsync("order-1", It.Is<string>(reason => reason.Contains("CheckoutExpired")), It.IsAny<CancellationToken>()));
    }
}
