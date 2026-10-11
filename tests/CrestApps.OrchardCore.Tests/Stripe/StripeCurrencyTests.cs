using CrestApps.OrchardCore.Stripe.Core;

namespace CrestApps.OrchardCore.Tests.Stripe;

public sealed class StripeCurrencyTests
{
    [Theory]
    [InlineData("USD", 2)]
    [InlineData("EUR", 2)]
    [InlineData("JPY", 0)]
    [InlineData("KRW", 0)]
    [InlineData("KWD", 3)]
    [InlineData("BHD", 3)]
    [InlineData("UGX", 2)]
    [InlineData("ISK", 2)]
    [InlineData("unknown", 2)]
    public void GetDecimalPlaces_ReturnsStripeSpecificPrecision(string currency, int expected)
    {
        // Act
        var places = StripeCurrency.GetDecimalPlaces(currency);

        // Assert
        Assert.Equal(expected, places);
    }

    [Theory]
    [InlineData(10.00, "USD", 1000)]
    [InlineData(10.005, "USD", 1001)]
    [InlineData(500, "JPY", 500)]
    [InlineData(1.005, "KWD", 1010)]
    [InlineData(1.001, "KWD", 1000)]
    public void ToMinorUnits_ConvertsUsingCurrencyPrecision(decimal amount, string currency, long expected)
    {
        // Act
        var minor = StripeCurrency.ToMinorUnits(amount, currency);

        // Assert
        Assert.Equal(expected, minor);
    }

    [Fact]
    public void ToMinorUnits_ForThreeDecimalCurrency_IsAlwaysAMultipleOfTen()
    {
        // Act
        var minor = StripeCurrency.ToMinorUnits(1.234m, "KWD");

        // Assert
        Assert.Equal(0, minor % 10);
    }

    [Theory]
    [InlineData(1000, "USD", 10.00)]
    [InlineData(500, "JPY", 500)]
    [InlineData(1010, "KWD", 1.01)]
    public void FromMinorUnits_ConvertsBackToMajorUnits(long minor, string currency, decimal expected)
    {
        // Act
        var major = StripeCurrency.FromMinorUnits(minor, currency);

        // Assert
        Assert.Equal(expected, major);
    }

    [Theory]
    [InlineData(10.00, "USD")]
    [InlineData(500, "JPY")]
    [InlineData(29.99, "EUR")]
    public void RoundTrip_PreservesTwoAndZeroDecimalAmounts(decimal amount, string currency)
    {
        // Act
        var minor = StripeCurrency.ToMinorUnits(amount, currency);
        var back = StripeCurrency.FromMinorUnits(minor, currency);

        // Assert
        Assert.Equal(amount, back);
    }
}
