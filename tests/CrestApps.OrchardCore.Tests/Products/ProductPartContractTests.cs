using CrestApps.OrchardCore.Payments.Core.Models;
using CrestApps.OrchardCore.Products.Core.Models;

namespace CrestApps.OrchardCore.Tests.Products;

public sealed class ProductPartContractTests
{
    [Fact]
    public void ProductPart_KeepsItsSerializationName()
    {
        // The content part name is the CLR type name, which is the key ProductPart content is stored under.
        // Renaming or moving the type would silently orphan every existing product, so the name is a
        // contract that must not change.
        Assert.Equal("ProductPart", typeof(ProductPart).Name);
    }

    [Fact]
    public void ProductPart_StaysInItsOriginalNamespace()
    {
        // Physically relocating the type across assemblies is a source-breaking change for downstream
        // consumers. The sellable seam is added instead, so the namespace stays stable.
        Assert.Equal("CrestApps.OrchardCore.Payments.Core.Models", typeof(ProductPart).Namespace);
    }

    [Fact]
    public void ProductPart_ExposesAdditiveSkuProperty()
    {
        // Arrange & Act
        var part = new ProductPart
        {
            Price = 9.99,
            Sku = "SKU-1",
        };

        // Assert
        Assert.Equal("SKU-1", part.Sku);
    }

    [Fact]
    public void SellableProduct_CarriesAResolvedPurchasableSnapshot()
    {
        // Arrange & Act
        var product = new SellableProduct
        {
            ContentItemId = "ci-1",
            ContentType = "Product",
            Sku = "SKU-1",
            Title = "Widget",
            UnitPrice = 9.99m,
            Currency = "USD",
        };

        // Assert
        Assert.IsAssignableFrom<ISellableProduct>(product);
        Assert.Equal("ci-1", product.ContentItemId);
        Assert.Equal(9.99m, product.UnitPrice);
        Assert.Equal("USD", product.Currency);
    }
}
