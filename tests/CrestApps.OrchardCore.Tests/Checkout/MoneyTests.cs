using CrestApps.OrchardCore.Payments;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Checkout;

public sealed class MoneyTests
{
    [Fact]
    public void AreEqual_WhenFloatingPointDrift_TreatsAmountsAsEqual()
    {
        // Arrange: 19.99 + 10.00 is not exactly 29.99 in binary floating point.
        var computed = 19.99 + 10.00;

        // Act & Assert
        Assert.True(Money.AreEqual(computed, 29.99, "USD"));
    }

    [Fact]
    public void AreEqual_WhenSubCentDifference_ForTwoDecimalCurrency_TreatsAsEqual()
    {
        // Act & Assert
        Assert.True(Money.AreEqual(10.001, 10.002, "USD"));
    }

    [Fact]
    public void AreEqual_WhenDifferentInMinorUnits_ReturnsFalse()
    {
        // Act & Assert
        Assert.False(Money.AreEqual(10.00, 10.01, "USD"));
    }

    [Fact]
    public void AreEqual_ForZeroDecimalCurrency_IgnoresFractions()
    {
        // Act & Assert: JPY has no minor unit, so 100.4 and 100 settle identically.
        Assert.True(Money.AreEqual(100.4, 100.0, "JPY"));
    }

    [Fact]
    public void AreEqual_Nullable_WhenEitherNull_ReturnsFalse()
    {
        // Act & Assert
        Assert.False(Money.AreEqual(null, 10.00, "USD"));
        Assert.False(Money.AreEqual(10.00, null, "USD"));
        Assert.False(Money.AreEqual((double?)null, null, "USD"));
    }

    [Fact]
    public void IsGreaterThan_UsesMinorUnitComparison()
    {
        // Act & Assert
        Assert.True(Money.IsGreaterThan(10.02, 10.01, "USD"));
        Assert.False(Money.IsGreaterThan(10.001, 10.002, "USD"));
    }

    [Fact]
    public void ToMinorUnits_ForThreeDecimalCurrency_UsesThousandths()
    {
        // Act & Assert
        Assert.Equal(1235, Money.ToMinorUnits(1.2345, "KWD"));
    }

    [Theory]
    [InlineData("JPY", 1000d, 1000)]
    [InlineData("KRW", 1000d, 1000)]
    [InlineData("MGA", 1.55d, 155)]
    [InlineData("IQD", 1.234d, 1234)]
    [InlineData("LYD", 1.234d, 1234)]
    [InlineData("CLF", 1.2345d, 12345)]
    [InlineData("UYW", 1.2345d, 12345)]
    [InlineData("USD", 10.005d, 1001)]
    public void ToMinorUnits_UsesIso4217Precision_ForEveryCurrencyClass(string currency, double amount, long expected)
    {
        // Act & Assert
        Assert.Equal(expected, Money.ToMinorUnits(amount, currency));
    }
}
