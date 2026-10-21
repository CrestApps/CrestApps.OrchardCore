using CrestApps.OrchardCore.Products.Core.Services;
using Moq;

namespace CrestApps.OrchardCore.Tests.Products;

/// <summary>
/// An amount is shown and kept in the shopper's currency when it can be converted, and as it is otherwise.
/// </summary>
public sealed class ShopperCurrencyExtensionsTests
{
    [Fact]
    public async Task FormatForShopperAsync_ShowsTheAmountInTheShoppersCurrency()
    {
        // Arrange
        var shopperCurrency = ShopperCurrency("EUR", (amount, _, _) => amount * 2m);

        // Act
        var shown = await shopperCurrency.FormatForShopperAsync(10m, "USD", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ProductPriceFormatter.Format(20m, "EUR"), shown);
    }

    [Fact]
    public async Task FormatForShopperAsync_ShowsTheAmountAsItIs_WhenItCantBeConverted()
    {
        // Arrange
        var shopperCurrency = ShopperCurrency("EUR", (_, _, _) => null);

        // Act
        var shown = await shopperCurrency.FormatForShopperAsync(10m, "USD", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ProductPriceFormatter.Format(10m, "USD"), shown);
    }

    [Theory]
    [InlineData(null, "EUR")]
    [InlineData("USD", null)]
    [InlineData("usd", "USD")]
    public async Task ConvertOrKeepAsync_KeepsTheAmount_WithoutTwoCurrencies(string fromCurrency, string toCurrency)
    {
        // Arrange
        var shopperCurrency = ShopperCurrency("EUR", (amount, _, _) => amount * 2m);

        // Act
        var amount = await shopperCurrency.ConvertOrKeepAsync(10m, fromCurrency, toCurrency, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(10m, amount);
    }

    [Fact]
    public async Task ConvertOrKeepAsync_ConvertsTheAmount_OrKeepsItWhenItCantBe()
    {
        // Arrange
        var converting = ShopperCurrency("EUR", (amount, _, _) => amount * 2m);
        var failing = ShopperCurrency("EUR", (_, _, _) => null);

        // Act
        var converted = await converting.ConvertOrKeepAsync(10m, "USD", "EUR", isPrice: false, TestContext.Current.CancellationToken);
        var kept = await failing.ConvertOrKeepAsync(10m, "USD", "EUR", isPrice: false, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(20m, converted);
        Assert.Equal(10m, kept);
    }

    private static IShopperCurrency ShopperCurrency(string currency, Func<decimal, string, string, decimal?> convert)
    {
        var shopperCurrency = new Mock<IShopperCurrency>();
        shopperCurrency
            .Setup(value => value.GetCurrencyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(currency);
        shopperCurrency
            .Setup(value => value.ConvertAsync(It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns((decimal amount, string from, string to, bool _, CancellationToken _) => ValueTask.FromResult(convert(amount, from, to)));

        return shopperCurrency.Object;
    }
}
