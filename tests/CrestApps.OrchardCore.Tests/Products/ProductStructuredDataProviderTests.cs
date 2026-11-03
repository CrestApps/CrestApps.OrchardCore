using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Products.StructuredData;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Products;

public sealed class ProductStructuredDataProviderTests
{
    private static readonly DateTime _now = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void GetPrice_UsesTheProductPriceAndItsCurrency()
    {
        // Arrange
        var part = new ProductPart
        {
            Price = 25m,
            Currency = "usd",
        };

        // Act
        var price = ProductStructuredDataProvider.GetPrice(new ContentItem(), part, variant: null, _now);

        // Assert
        Assert.Equal((25m, "USD"), price);
    }

    [Fact]
    public void GetPrice_WhileASaleIsOn_UsesTheSalePrice()
    {
        // Arrange
        var part = new ProductPart
        {
            Price = 25m,
            SalePrice = 20m,
            SaleStartsUtc = _now.AddDays(-1),
            SaleEndsUtc = _now.AddDays(1),
            Currency = "USD",
        };

        // Act
        var price = ProductStructuredDataProvider.GetPrice(new ContentItem(), part, variant: null, _now);

        // Assert
        Assert.Equal(20m, price?.Amount);
    }

    [Fact]
    public void GetPrice_WhenTheProductHasNoPriceOrCurrency_ReturnsNull()
    {
        Assert.Null(ProductStructuredDataProvider.GetPrice(new ContentItem(), new ProductPart { Currency = "USD" }, null, _now));
        Assert.Null(ProductStructuredDataProvider.GetPrice(new ContentItem(), new ProductPart { Price = 5m }, null, _now));
    }

    [Fact]
    public void ToAvailability_WhenNoProviderTracksStock_IsInStock()
    {
        Assert.Equal("https://schema.org/InStock", ProductStructuredDataProvider.ToAvailability(null, canBuy: true));
    }

    [Theory]
    [InlineData(10, false, false, true, "https://schema.org/InStock")]
    [InlineData(2, false, false, true, "https://schema.org/LimitedAvailability")]
    [InlineData(0, false, false, true, "https://schema.org/OutOfStock")]
    [InlineData(0, true, false, true, "https://schema.org/BackOrder")]
    [InlineData(0, true, true, true, "https://schema.org/PreOrder")]
    [InlineData(10, false, false, false, "https://schema.org/OutOfStock")]
    public void ToAvailability_MapsTheStockStatusToSchemaOrg(int available, bool allowBackorder, bool hasReleaseDate, bool canBuy, string expected)
    {
        // Arrange
        var availability = new ProductAvailability(available, allowBackorder, LowStockThreshold: 5)
        {
            ReleaseDateUtc = hasReleaseDate ? _now.AddDays(30) : null,
        };

        // Act & Assert
        Assert.Equal(expected, ProductStructuredDataProvider.ToAvailability(availability, canBuy));
    }

    [Theory]
    [InlineData("new", "https://schema.org/NewCondition")]
    [InlineData("Used", "https://schema.org/UsedCondition")]
    [InlineData("refurbished", "https://schema.org/RefurbishedCondition")]
    [InlineData("broken", null)]
    [InlineData(null, null)]
    public void ToItemCondition_MapsTheFeedConditionToSchemaOrg(string condition, string expected)
    {
        Assert.Equal(expected, ProductStructuredDataProvider.ToItemCondition(condition));
    }
}
