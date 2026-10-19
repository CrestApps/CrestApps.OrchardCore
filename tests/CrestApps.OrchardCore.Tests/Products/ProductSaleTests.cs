using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;

namespace CrestApps.OrchardCore.Tests.Products;

/// <summary>
/// A product sells at its sale price only while its sale runs and only when that price is lower; a variant priced on
/// its own needs its own sale price; and during a sale the regular price is shown struck through.
/// </summary>
public sealed class ProductSaleTests
{
    private static readonly DateTime _now = new(2026, 11, 27, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void TheSalePriceAppliesOnlyInsideTheWindow()
    {
        var product = new ProductPart { Price = 100m, SalePrice = 80m, SaleStartsUtc = _now.AddHours(-1), SaleEndsUtc = _now.AddHours(1) };

        Assert.Equal(80m, ProductSale.GetPrice(product, null, _now));
        Assert.Equal(100m, ProductSale.GetPrice(product, null, _now.AddHours(-2)));
        Assert.Equal(100m, ProductSale.GetPrice(product, null, _now.AddHours(1)));
    }

    [Fact]
    public void ASaleWithoutDatesRunsUntilTheSalePriceIsRemoved()
    {
        var product = new ProductPart { Price = 100m, SalePrice = 80m };

        Assert.Equal(80m, ProductSale.GetPrice(product, null, _now));
        Assert.Equal(100m, ProductSale.GetCompareAtPrice(product, null, _now));
    }

    [Fact]
    public void ASalePriceThatIsntLowerIsIgnored()
    {
        var product = new ProductPart { Price = 100m, SalePrice = 120m };

        Assert.Equal(100m, ProductSale.GetPrice(product, null, _now));
        Assert.Null(ProductSale.GetSalePrice(product, null, _now));
    }

    [Fact]
    public void DuringASaleTheHigherOfTheCompareAtAndRegularPriceIsStruckThrough()
    {
        var product = new ProductPart { Price = 100m, CompareAtPrice = 130m, SalePrice = 80m };

        Assert.Equal(130m, ProductSale.GetCompareAtPrice(product, null, _now));
        Assert.Equal(130m, ProductSale.GetCompareAtPrice(new ProductPart { Price = 100m, CompareAtPrice = 130m }, null, _now));
    }

    [Fact]
    public void AVariantAtTheProductsPriceSharesItsSale_OnePricedOnItsOwnNeedsItsOwn()
    {
        var product = new ProductPart { Price = 100m, SalePrice = 80m };
        var inherited = new ProductVariant { VariantId = "s" };
        var ownPrice = new ProductVariant { VariantId = "xl", Price = 120m };
        var ownSale = new ProductVariant { VariantId = "xxl", Price = 120m, SalePrice = 90m };

        Assert.Equal(80m, ProductSale.GetPrice(product, inherited, _now));
        Assert.Equal(120m, ProductSale.GetPrice(product, ownPrice, _now));
        Assert.Equal(90m, ProductSale.GetPrice(product, ownSale, _now));
        Assert.Equal(120m, ProductSale.GetCompareAtPrice(product, ownSale, _now));
    }

    [Fact]
    public void HasSalePriceLooksAtTheVariantsToo()
    {
        Assert.False(ProductSale.HasSalePrice(new ProductPart { Price = 10m }, [new ProductVariant()]));
        Assert.True(ProductSale.HasSalePrice(new ProductPart { Price = 10m }, [new ProductVariant { SalePrice = 8m }]));
    }
}
