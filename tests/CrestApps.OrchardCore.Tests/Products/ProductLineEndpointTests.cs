using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Endpoints;
using CrestApps.OrchardCore.Taxation.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Products;

/// <summary>
/// A document line editor's product picker fills a line from the product: its name, SKU, price and tax category.
/// </summary>
public sealed class ProductLineEndpointTests
{
    [Fact]
    public void ToLine_GivesTheProductsName_Sku_Price_AndTaxCategory()
    {
        // Arrange
        var product = new ContentItem { ContentItemId = "product-7", ContentType = "StoreProduct", DisplayText = "Field Watch" };
        product.Alter<ProductPart>(part =>
        {
            part.Price = 189m;
            part.Currency = "USD";
            part.Sku = "WAT-7";
        });
        product.Alter<TaxationPart>(part => part.TaxCategoryCode = "standard");

        // Act
        var line = ProductSearchEndpoints.ToLine(product);

        // Assert
        Assert.Equal(("product-7", "Field Watch", "WAT-7", 189m, "USD", "standard"), (line.Id, line.Name, line.Sku, line.Price, line.Currency, line.TaxCategoryCode));
    }

    [Fact]
    public void ToLine_ForAProductThatIsNotTaxable_GivesNoTaxCategory()
    {
        // Arrange
        var product = new ContentItem { ContentItemId = "product-8", ContentType = "StoreProduct", DisplayText = "Gift card" };
        product.Alter<ProductPart>(part => part.Price = 50m);
        product.Alter<TaxationPart>(part =>
        {
            part.Taxable = false;
            part.TaxCategoryCode = "standard";
        });

        // Act
        var line = ProductSearchEndpoints.ToLine(product);

        // Assert
        Assert.Null(line.TaxCategoryCode);
        Assert.Equal(50m, line.Price);
    }
}
