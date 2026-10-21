using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Services;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Products;

/// <summary>
/// The cart offers what its products are sold with: their cross-sells, the most recommended first, never something
/// already in the cart.
/// </summary>
public sealed class ProductRecommendationTests
{
    [Fact]
    public void CrossSellsComeFromTheCartsProducts_MostRecommendedFirst()
    {
        var watch = Product("watch", crossSells: ["strap", "case", "cloth"]);
        var bag = Product("bag", crossSells: ["cloth", "strap"]);

        var ids = ProductRecommendationService.GetCrossSellIds([watch, bag]);

        Assert.Equal(["strap", "cloth", "case"], ids);
    }

    [Fact]
    public void WhatsAlreadyInTheCartIsntOffered()
    {
        var watch = Product("watch", crossSells: ["bag", "strap"]);
        var bag = Product("bag", crossSells: ["watch"]);

        Assert.Equal(["strap"], ProductRecommendationService.GetCrossSellIds([watch, bag]));
    }

    [Fact]
    public void AtMostTheAskedForNumberAreOffered()
    {
        var watch = Product("watch", crossSells: ["a", "b", "c", "d", "e", "f"]);

        Assert.Equal(4, ProductRecommendationService.GetCrossSellIds([watch]).Count);
        Assert.Empty(ProductRecommendationService.GetCrossSellIds([Product("plain", crossSells: [])]));
    }

    private static ContentItem Product(string id, string[] crossSells)
    {
        var item = new ContentItem { ContentItemId = id, ContentType = "Product" };
        item.Alter<ProductRecommendationsPart>(part => part.CrossSellIds = [.. crossSells]);

        return item;
    }
}
