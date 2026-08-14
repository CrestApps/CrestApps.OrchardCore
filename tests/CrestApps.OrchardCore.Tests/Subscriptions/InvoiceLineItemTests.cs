using CrestApps.OrchardCore.Subscriptions.Core.Models;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

public class InvoiceLineItemTests
{
    [Theory]
    [InlineData(1, 19.99, 19.99)]
    [InlineData(3, 10.00, 30.00)]
    [InlineData(2, 19.995, 39.99)]
    [InlineData(0, 19.99, 0.00)]
    public void GetLineTotal_MultipliesQuantityByUnitPriceAndRoundsToCents(int quantity, double unitPrice, double expected)
    {
        var lineItem = new InvoiceLineItem
        {
            Quantity = quantity,
            UnitPrice = unitPrice,
        };

        Assert.Equal(expected, lineItem.GetLineTotal());
    }
}
