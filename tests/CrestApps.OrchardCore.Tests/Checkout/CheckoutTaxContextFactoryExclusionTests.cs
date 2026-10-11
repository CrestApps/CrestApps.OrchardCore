using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Services;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// Paying an outstanding balance through the checkout must not tax it again: the balance already carries the tax
/// decided when it was raised.
/// </summary>
public sealed class CheckoutTaxContextFactoryExclusionTests
{
    [Fact]
    public void Create_LeavesOutLinesWhoseTaxWasAlreadyDecided()
    {
        // Arrange
        var invoice = new CheckoutInvoice
        {
            Currency = "USD",
            LineItems =
            [
                new CheckoutLineItem { ItemId = "balance", Quantity = 1, UnitPrice = 108m, ExcludeFromTax = true },
                new CheckoutLineItem { ItemId = "new-sale", Quantity = 1, UnitPrice = 20m },
            ],
        };

        // Act
        var context = CheckoutTaxContextFactory.Create(invoice, profile: null, DateTime.UtcNow);

        // Assert
        var item = Assert.Single(context.Items);

        Assert.Equal("new-sale", item.Id);
    }
}
