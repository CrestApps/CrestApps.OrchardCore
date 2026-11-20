using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Orders;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using Moq;

namespace CrestApps.OrchardCore.Tests.AgenticCommerce;

/// <summary>
/// An agent sees each line of its order by the name the order stores, which already names the variant.
/// </summary>
public sealed class AgentOrderViewBuilderTests
{
    [Fact]
    public async Task BuildAsync_VariantLine_NamesTheVariantOnce()
    {
        // Arrange
        var links = new Mock<IOrderLinkService>();

        links.Setup(service => service.CreateUrlAsync(It.IsAny<Order>())).ReturnsAsync("https://store.test/orders/order-1");

        var builder = new AgentOrderViewBuilder(Mock.Of<IAgentItemResolver>(), [], [links.Object], null);
        var order = new Order
        {
            ItemId = "order-1",
            OrderNumber = "261120-ABC",
            Currency = "USD",
            Status = OrderStatus.Paid,
            Lines =
            [
                new OrderLine { LineId = "l1", ContentItemId = "p1", VariantId = "v1", Title = "Tee (White / S)", VariantTitle = "White / S", Quantity = 1 },
                new OrderLine { LineId = "l2", ContentItemId = "p2", Title = "Mug", Quantity = 1 },
            ],
        };

        // Act
        var view = await builder.BuildAsync(order, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["Tee (White / S)", "Mug"], view.Lines.Select(line => line.Title));
    }
}
