using CrestApps.OrchardCore.Products.Core.Models;

namespace CrestApps.OrchardCore.Tests.Products;

/// <summary>
/// An external product is bought where it links to, so it can't be bought here whatever its type's sales mode.
/// </summary>
public sealed class ExternalProductTests
{
    [Theory]
    [InlineData(ProductSalesMode.Purchasable)]
    [InlineData(ProductSalesMode.PerItem)]
    public void AnExternalProductCantBeBoughtHere(ProductSalesMode mode)
    {
        var settings = new ProductPartSettings { SalesMode = mode };

        Assert.True(new ProductPart { Price = 10m }.CanBePurchased(settings));
        Assert.False(new ProductPart { Price = 10m, ExternalUrl = "https://partner.example.test/item" }.CanBePurchased(settings));
    }
}
