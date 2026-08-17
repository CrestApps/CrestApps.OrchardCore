using CrestApps.OrchardCore.Payments;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Checkout;

public sealed class MoneyTests
{
    [Fact]
    public void AreEqual_WhenFloatingPointDrift_TreatsAmountsAsEqual()
    {
        // Arrange: Money compares at whole minor-unit precision regardless of fractional noise.
        var computed = 19.99m + 10.00m;

        // Act & Assert
        Assert.True(Money.AreEqual(computed, 29.99m, "USD"));
    }

    [Fact]
    public void AreEqual_WhenSubCentDifference_ForTwoDecimalCurrency_TreatsAsEqual()
    {
        // Act & Assert
        Assert.True(Money.AreEqual(10.001m, 10.002m, "USD"));
    }

    [Fact]
    public void AreEqual_WhenDifferentInMinorUnits_ReturnsFalse()
    {
        // Act & Assert
        Assert.False(Money.AreEqual(10.00m, 10.01m, "USD"));
    }

    [Fact]
    public void AreEqual_ForZeroDecimalCurrency_IgnoresFractions()
    {
        // Act & Assert: JPY has no minor unit, so 100.4 and 100 settle identically.
        Assert.True(Money.AreEqual(100.4m, 100.0m, "JPY"));
    }

    [Fact]
    public void AreEqual_Nullable_WhenEitherNull_ReturnsFalse()
    {
        // Act & Assert
        Assert.False(Money.AreEqual(null, 10.00m, "USD"));
        Assert.False(Money.AreEqual(10.00m, null, "USD"));
        Assert.False(Money.AreEqual((decimal?)null, null, "USD"));
    }

    [Fact]
    public void IsGreaterThan_UsesMinorUnitComparison()
    {
        // Act & Assert
        Assert.True(Money.IsGreaterThan(10.02m, 10.01m, "USD"));
        Assert.False(Money.IsGreaterThan(10.001m, 10.002m, "USD"));
    }

    [Fact]
    public void ToMinorUnits_ForThreeDecimalCurrency_UsesThousandths()
    {
        // Act & Assert
        Assert.Equal(1235, Money.ToMinorUnits(1.2345m, "KWD"));
    }

    [Theory]
    [InlineData("JPY", 1000, 1000)]
    [InlineData("KRW", 1000, 1000)]
    [InlineData("MGA", 1.55, 155)]
    [InlineData("IQD", 1.234, 1234)]
    [InlineData("LYD", 1.234, 1234)]
    [InlineData("CLF", 1.2345, 12345)]
    [InlineData("UYW", 1.2345, 12345)]
    [InlineData("USD", 10.005, 1001)]
    public void ToMinorUnits_UsesIso4217Precision_ForEveryCurrencyClass(string currency, double amount, long expected)
    {
        // Act & Assert
        Assert.Equal(expected, Money.ToMinorUnits((decimal)amount, currency));
    }
}
