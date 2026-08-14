using CrestApps.OrchardCore.Stripe.Core;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

public class StripeCurrencyTests
{
    [Theory]
    [InlineData("USD", 2)]
    [InlineData("usd", 2)]
    [InlineData("EUR", 2)]
    [InlineData("JPY", 0)]
    [InlineData("KRW", 0)]
    [InlineData("XOF", 0)]
    [InlineData("UGX", 2)] // Stripe special case: sent as a two-decimal value divisible by 100.
    [InlineData("BHD", 3)]
    [InlineData("KWD", 3)]
    [InlineData("ZZZ", 2)] // Unknown currencies default to two decimals.
    public void GetDecimalPlaces_ReturnsCurrencyExponent(string currency, int expected)
    {
        Assert.Equal(expected, StripeCurrency.GetDecimalPlaces(currency));
    }

    [Theory]
    [InlineData(10.00, "USD", 1000)]
    [InlineData(19.99, "USD", 1999)]
    [InlineData(0.50, "EUR", 50)]
    public void ToMinorUnits_TwoDecimalCurrency_MultipliesByHundred(double amount, string currency, long expected)
    {
        Assert.Equal(expected, StripeCurrency.ToMinorUnits(amount, currency));
    }

    [Theory]
    [InlineData(500, "JPY", 500)]
    [InlineData(50, "JPY", 50)]
    [InlineData(1000, "KRW", 1000)]
    public void ToMinorUnits_ZeroDecimalCurrency_DoesNotMultiply(double amount, string currency, long expected)
    {
        // Regression guard: a ¥500 price must be sent to Stripe as 500, not 50000.
        Assert.Equal(expected, StripeCurrency.ToMinorUnits(amount, currency));
    }

    [Theory]
    [InlineData(1.000, "KWD", 1000)]
    [InlineData(1.234, "KWD", 1230)] // Stripe requires three-decimal amounts to be a multiple of ten.
    [InlineData(1.235, "KWD", 1240)]
    [InlineData(1.2349, "KWD", 1230)] // No double-rounding: rounds directly at 0.01 granularity.
    public void ToMinorUnits_ThreeDecimalCurrency_RoundsToMultipleOfTen(double amount, string currency, long expected)
    {
        Assert.Equal(expected, StripeCurrency.ToMinorUnits(amount, currency));
    }

    [Fact]
    public void ToMinorUnits_DoesNotTruncateCents()
    {
        // (int)(19.99 * 100) truncates to 1998; away-from-zero rounding must yield 1999.
        Assert.Equal(1999, StripeCurrency.ToMinorUnits(19.99m, "USD"));
    }

    [Theory]
    [InlineData(1000, "USD", 10.00)]
    [InlineData(1999, "USD", 19.99)]
    [InlineData(500, "JPY", 500)]
    [InlineData(1000, "KWD", 1.000)]
    public void FromMinorUnits_ReversesConversion(long minorUnits, string currency, double expected)
    {
        Assert.Equal((decimal)expected, StripeCurrency.FromMinorUnits(minorUnits, currency));
    }

    [Fact]
    public void RoundTrip_ZeroDecimalCurrency_IsStable()
    {
        var minor = StripeCurrency.ToMinorUnits(500m, "JPY");
        Assert.Equal(500, minor);
        Assert.Equal(500m, StripeCurrency.FromMinorUnits(minor, "JPY"));
    }
}
