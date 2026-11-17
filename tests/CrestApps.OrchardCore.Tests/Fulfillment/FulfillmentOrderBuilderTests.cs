using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Fulfillment;
using CrestApps.OrchardCore.Fulfillment.Core.Models;
using CrestApps.OrchardCore.Fulfillment.Core.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Shipping.Models;
using CrestApps.OrchardCore.Shipping.Services;

namespace CrestApps.OrchardCore.Tests.Fulfillment;

/// <summary>
/// Only a paid order with a shipping address goes to partners; it is described with the order's own address, and with
/// only the physical units Shipping has not put in a shipment yet.
/// </summary>
public sealed class FulfillmentOrderBuilderTests
{
    [Fact]
    public void CheckOrder_PaidOrderThatShips_MaySend()
    {
        // Act
        var blocked = FulfillmentOrderBuilder.CheckOrder(PaidOrder());

        // Assert
        Assert.Null(blocked);
    }

    [Theory]
    [InlineData(OrderStatus.AwaitingPayment)]
    [InlineData(OrderStatus.Canceled)]
    public void CheckOrder_OrderNotPaid_AsksAPerson(OrderStatus status)
    {
        // Arrange
        var order = PaidOrder();

        order.Status = status;

        // Act
        var blocked = FulfillmentOrderBuilder.CheckOrder(order);

        // Assert
        Assert.Equal(FulfillmentReasons.NotPaid, blocked.Reason);
        Assert.Equal([status.ToString()], blocked.Arguments);
        Assert.False(blocked.NothingToSend);
    }

    [Fact]
    public void CheckOrder_NoShippingAddress_AsksAPerson()
    {
        // Arrange
        var order = PaidOrder();

        order.ShippingAddress = null;

        // Act
        var blocked = FulfillmentOrderBuilder.CheckOrder(order);

        // Assert
        Assert.Equal(FulfillmentReasons.NoAddress, blocked.Reason);
        Assert.False(blocked.NothingToSend);
    }

    [Fact]
    public void Describe_SomeUnitsAlreadyShipped_DescribesOnlyWhatIsLeft()
    {
        // Arrange
        var order = PaidOrder();
        var shipped = new Shipment
        {
            Status = ShipmentStatus.Shipped,
            Lines = [new ShipmentLine { LineId = "l1", Quantity = 1 }],
        };

        // Act
        var check = FulfillmentOrderBuilder.Describe(order, DefaultShipmentService.GetRemaining(order, [shipped]));

        // Assert
        Assert.True(check.CanSend);

        var line = Assert.Single(check.Description.Lines);

        Assert.Equal(("l1", "p1", "v1", 2, "Tee (White / S)"), (line.LineId, line.ContentItemId, line.VariantId, line.Quantity, line.Title));
    }

    [Fact]
    public void Describe_EverythingInAShipment_HasNothingToSend()
    {
        // Arrange
        var order = PaidOrder();
        var pending = new Shipment
        {
            Status = ShipmentStatus.Pending,
            Lines = [new ShipmentLine { LineId = "l1", Quantity = 3 }],
        };

        // Act
        var check = FulfillmentOrderBuilder.Describe(order, DefaultShipmentService.GetRemaining(order, [pending]));

        // Assert
        Assert.False(check.CanSend);
        Assert.True(check.NothingToSend);
        Assert.Equal(FulfillmentReasons.NothingLeft, check.Reason);
    }

    [Fact]
    public void ForLines_UsesTheOrdersAddressEmailAndPhone()
    {
        // Arrange
        var order = PaidOrder();

        // Act
        var description = FulfillmentOrderBuilder.ForLines(order, new Dictionary<string, int> { ["l1"] = 1 });

        // Assert
        Assert.Same(order.ShippingAddress, description.ShippingAddress);
        Assert.Equal(("order-1", "261117-ABC", "test@example.com", "+15135550100"), (description.OrderId, description.OrderNumber, description.Email, description.Phone));
        Assert.Equal(1, Assert.Single(description.Lines).Quantity);
    }

    [Fact]
    public void ForLines_NoPhoneOnTheAddress_UsesTheCustomersPhone()
    {
        // Arrange
        var order = PaidOrder();

        order.ShippingAddress = new Address { Name = "Test Customer", AddressLine1 = "123 Test Street", Country = "US" };
        order.CustomerPhone = "+15135550199";

        // Act
        var description = FulfillmentOrderBuilder.ForLines(order, new Dictionary<string, int> { ["l1"] = 1 });

        // Assert
        Assert.Equal("+15135550199", description.Phone);
    }

    [Theory]
    [InlineData("Cher", "Cher", null)]
    [InlineData("  Ada   Lovelace ", "Ada", "Lovelace")]
    [InlineData("Mary Ann Evans", "Mary", "Ann Evans")]
    [InlineData("", null, null)]
    [InlineData(null, null, null)]
    public void Split_FirstWordAndTheRest_ASingleNameHasNoLastName(string name, string first, string last)
    {
        // Act
        var (firstName, lastName) = FulfillmentNames.Split(name);

        // Assert
        Assert.Equal((first, last), (firstName, lastName));
    }

    private static Order PaidOrder()
        => new()
        {
            ItemId = "order-1",
            OrderNumber = "261117-ABC",
            Status = OrderStatus.Paid,
            CustomerEmail = "test@example.com",
            CustomerPhone = "+15135550199",
            ShippingAddress = new Address
            {
                Name = "Test Customer",
                AddressLine1 = "123 Test Street",
                City = "Cincinnati",
                Region = "OH",
                PostalCode = "45202",
                Country = "US",
                Phone = "+15135550100",
            },
            Lines =
            [
                new OrderLine { LineId = "l1", ContentItemId = "p1", VariantId = "v1", Title = "Tee", VariantTitle = "White / S", Quantity = 3, ProductType = ProductType.Good },
                new OrderLine { LineId = "l2", ContentItemId = "d1", Title = "Digital art", Quantity = 1, ProductType = ProductType.Digital },
            ],
        };
}
