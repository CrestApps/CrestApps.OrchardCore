using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Fulfillment.Core.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Products.Core.Models;

namespace CrestApps.OrchardCore.Tests.Fulfillment;

/// <summary>
/// Only a paid order with something to ship and a shipping address goes to fulfillment partners, and it is described
/// with the buyer's address and its physical lines only.
/// </summary>
public sealed class FulfillmentOrderFactoryTests
{
    [Fact]
    public void Check_PaidOrderThatShips_DescribesTheRecipientAndThePhysicalLines()
    {
        // Arrange
        var order = PaidOrder();

        // Act
        var check = FulfillmentOrderFactory.Check(order);

        // Assert
        Assert.True(check.CanSend);
        Assert.Equal("order-1", check.Order.OrderId);
        Assert.Equal(("Test", "Customer Smith"), (check.Order.Recipient.FirstName, check.Order.Recipient.LastName));
        Assert.Equal(("Cincinnati", "OH", "45202", "US"), (check.Order.Recipient.City, check.Order.Recipient.Region, check.Order.Recipient.PostalCode, check.Order.Recipient.Country));
        Assert.Equal("test@example.com", check.Order.Recipient.Email);

        var line = Assert.Single(check.Order.Lines);

        Assert.Equal(("p1", "v1", 2, "Tee (White / S)"), (line.ContentItemId, line.VariantId, line.Quantity, line.Title));
    }

    [Theory]
    [InlineData(OrderStatus.AwaitingPayment)]
    [InlineData(OrderStatus.Canceled)]
    public void Check_OrderNotPaid_IsNotSent(OrderStatus status)
    {
        // Arrange
        var order = PaidOrder();

        order.Status = status;

        // Act
        var check = FulfillmentOrderFactory.Check(order);

        // Assert
        Assert.False(check.CanSend);
        Assert.False(check.NothingToSend);
    }

    [Fact]
    public void Check_NothingShips_HasNothingToSend()
    {
        // Arrange
        var order = PaidOrder();

        order.Lines.RemoveAll(line => line.ProductType == ProductType.Good);

        // Act
        var check = FulfillmentOrderFactory.Check(order);

        // Assert
        Assert.False(check.CanSend);
        Assert.True(check.NothingToSend);
    }

    [Fact]
    public void Check_NoShippingAddress_AsksAPerson()
    {
        // Arrange
        var order = PaidOrder();

        order.ShippingAddress = null;

        // Act
        var check = FulfillmentOrderFactory.Check(order);

        // Assert
        Assert.False(check.CanSend);
        Assert.False(check.NothingToSend);
    }

    [Theory]
    [InlineData("Cher", "Cher", "Cher")]
    [InlineData("  Ada   Lovelace ", "Ada", "Lovelace")]
    [InlineData("", null, null)]
    public void SplitName_SplitsTheFirstWordFromTheRest(string name, string first, string last)
    {
        // Act
        var (firstName, lastName) = FulfillmentOrderFactory.SplitName(name);

        // Assert
        Assert.Equal((first, last), (firstName, lastName));
    }

    private static Order PaidOrder()
        => new()
        {
            ItemId = "order-1",
            OrderNumber = "261117-ABC",
            Status = OrderStatus.Paid,
            CustomerName = "Someone Else",
            CustomerEmail = "test@example.com",
            ShippingAddress = new Address
            {
                Name = "Test Customer Smith",
                AddressLine1 = "123 Test Street",
                City = "Cincinnati",
                Region = "OH",
                PostalCode = "45202",
                Country = "US",
            },
            Lines =
            [
                new OrderLine { LineId = "l1", ContentItemId = "p1", VariantId = "v1", Title = "Tee", VariantTitle = "White / S", Quantity = 2, ProductType = ProductType.Good },
                new OrderLine { LineId = "l2", ContentItemId = "d1", Title = "Digital art", Quantity = 1, ProductType = ProductType.Digital },
            ],
        };
}
