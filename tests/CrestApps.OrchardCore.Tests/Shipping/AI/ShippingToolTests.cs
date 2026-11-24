using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Shipping;
using CrestApps.OrchardCore.Shipping.AI;
using CrestApps.OrchardCore.Shipping.Models;
using CrestApps.OrchardCore.Shipping.Services;
using CrestApps.OrchardCore.Tests.Agent;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Security.Permissions;
using YesSqlSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Shipping.AI;

/// <summary>
/// The shipment, label and rate tools refuse a caller who is not signed in or lacks the page's permission, report a
/// missing shipment, and buy a label only for a rate quoted for that shipment, at the quoted price.
/// </summary>
public sealed class ShippingToolTests
{
    private readonly Shipment _shipment = new()
    {
        ItemId = "shipment-1",
        OrderId = "order-1",
        OrderNumber = "1051",
        Number = 1,
        Status = ShipmentStatus.Pending,
        Lines = [new ShipmentLine { LineId = "line-1", Title = "Mug", Quantity = 2 }],
    };

    private readonly Mock<IShipmentStore> _shipments = new();
    private readonly Mock<IShipmentService> _shipmentService = new();
    private readonly Mock<IOrderStore> _orders = new();
    private readonly Mock<ICarrierProviderResolver> _carriers = new();

    public ShippingToolTests()
    {
        _shipments.Setup(store => store.FindByIdAsync("shipment-1", It.IsAny<CancellationToken>())).ReturnsAsync(_shipment);
    }

    public static TheoryData<string, string> Tools => new()
    {
        { GetOrderFulfillmentTool.TheName, ShippingPermissions.FulfillOrders.Name },
        { CreateShipmentTool.TheName, ShippingPermissions.FulfillOrders.Name },
        { ShipShipmentTool.TheName, ShippingPermissions.FulfillOrders.Name },
        { UpdateShipmentStatusTool.TheName, ShippingPermissions.FulfillOrders.Name },
        { DeleteShipmentTool.TheName, ShippingPermissions.FulfillOrders.Name },
        { GetShipmentLabelRatesTool.TheName, ShippingPermissions.FulfillOrders.Name },
        { BuyShipmentLabelTool.TheName, ShippingPermissions.FulfillOrders.Name },
        { TestShippingRatesTool.TheName, ShippingPermissions.ManageShipping.Name },
    };

    [Theory]
    [MemberData(nameof(Tools))]
    public async Task Tool_RefusesACallerWhoIsNotSignedIn(string toolName, string permission)
    {
        // Arrange
        _ = permission;

        // Act
        var result = await InvokeAsync(toolName, Build(signedIn: false), []);

        // Assert
        Assert.Equal(ShippingAIToolBase.NotAllowedMessage, result);
        Assert.Empty(_shipments.Invocations);
        Assert.Empty(_shipmentService.Invocations);
    }

    [Theory]
    [MemberData(nameof(Tools))]
    public async Task Tool_RefusesACallerWithoutThePagesPermission(string toolName, string permission)
    {
        // Arrange
        var services = Build(authorize: (asked, _) => asked.Name != permission);

        // Act
        var result = await InvokeAsync(toolName, services, []);

        // Assert
        Assert.Equal(ShippingAIToolBase.NotAllowedMessage, result);
        Assert.Equal(permission, Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks).Permission);
        Assert.Empty(_shipments.Invocations);
        Assert.Empty(_orders.Invocations);
    }

    [Fact]
    public async Task DeleteShipment_SaysWhenTheShipmentIsMissing()
    {
        // Act
        var result = await InvokeAsync(DeleteShipmentTool.TheName, Build(), new() { ["shipmentId"] = "missing" });

        // Assert
        Assert.Equal("Nothing was done: no shipment has the id 'missing'. Use getOrderFulfillment to find it.", result);
        Assert.Empty(_shipmentService.Invocations);
    }

    [Fact]
    public async Task BuyShipmentLabel_RefusesARateThatWasNotQuoted_OrAnotherPrice()
    {
        // Arrange
        var services = Build();

        await ShipmentLabelQuotes.KeepAsync(services, new QuotedLabel
        {
            QuoteId = "quote-1",
            ShipmentId = "shipment-1",
            Rates = [new QuotedLabelRate { RateId = "rate-1", Carrier = "UPS", Service = "Ground", Amount = 9.85m, Currency = "USD" }],
        }, TestContext.Current.CancellationToken);

        // Act
        var unquoted = await InvokeAsync(BuyShipmentLabelTool.TheName, services, Buy("quote-2", "rate-1", 9.85m));
        var otherPrice = await InvokeAsync(BuyShipmentLabelTool.TheName, services, Buy("quote-1", "rate-1", 5m));

        // Assert
        Assert.StartsWith("Nothing was bought: that quote is not one getShipmentLabelRates gave for this shipment", unquoted, StringComparison.Ordinal);
        Assert.Equal("Nothing was bought: the UPS Ground rate costs 9.85 USD, not what was given. Confirm that price.", otherPrice);
        Assert.Empty(_carriers.Invocations);
    }

    private static Dictionary<string, object> Buy(string quoteId, string rateId, decimal amount)
        => new()
        {
            ["shipmentId"] = "shipment-1",
            ["quoteId"] = quoteId,
            ["rateId"] = rateId,
            ["amount"] = amount,
            ["currency"] = "USD",
            ["idempotencyKey"] = $"label-{quoteId}-{amount}",
        };

    private ServiceProvider Build(bool signedIn = true, Func<Permission, object, bool> authorize = null)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);

        services.AddSingleton(_shipments.Object);
        services.AddSingleton(_shipmentService.Object);
        services.AddSingleton(_orders.Object);
        services.AddSingleton(_carriers.Object);
        services.AddSingleton(Mock.Of<YesSqlSession>());
        services.AddSingleton<IDistributedCache>(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));

        return services.BuildServiceProvider();
    }

    private static Task<string> InvokeAsync(string toolName, IServiceProvider services, Dictionary<string, object> arguments)
    {
        AIFunction tool = toolName switch
        {
            GetOrderFulfillmentTool.TheName => new GetOrderFulfillmentTool(),
            CreateShipmentTool.TheName => new CreateShipmentTool(),
            ShipShipmentTool.TheName => new ShipShipmentTool(),
            UpdateShipmentStatusTool.TheName => new UpdateShipmentStatusTool(),
            DeleteShipmentTool.TheName => new DeleteShipmentTool(),
            GetShipmentLabelRatesTool.TheName => new GetShipmentLabelRatesTool(),
            BuyShipmentLabelTool.TheName => new BuyShipmentLabelTool(),
            _ => new TestShippingRatesTool(),
        };

        return AgentToolTestContext.InvokeAsync(tool, services, arguments);
    }
}
