using CrestApps.OrchardCore.Subscriptions.Core;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

public class MoneyTests
{
    [Theory]
    [InlineData(29.994, 29.99)]
    [InlineData(29.995, 30.00)]
    [InlineData(0.005, 0.01)]
    [InlineData(10, 10.00)]
    public void Round_RoundsToTwoDecimalsAwayFromZero(double amount, double expected)
    {
        Assert.Equal(expected, Money.Round(amount));
    }

    [Theory]
    [InlineData(29.99, 2999)]
    [InlineData(0.5, 50)]
    [InlineData(10, 1000)]
    [InlineData(0.005, 1)]
    public void ToMinorUnits_ConvertsMajorUnitsToWholeMinorUnits(double amount, long expected)
    {
        Assert.Equal(expected, Money.ToMinorUnits(amount));
    }

    [Fact]
    public void AreEqual_TreatsAccumulatedFloatingPointSumsAsEqual()
    {
        // 19.99 + 10.00 is not guaranteed to be bit-identical to 29.99 in binary floating point.
        // The default '==' operator can therefore report a valid payment as a mismatch; Money must not.
        var summed = 19.99 + 10.00;

        Assert.True(Money.AreEqual(summed, 29.99));
    }

    [Theory]
    [InlineData(29.99, 29.99, true)]
    [InlineData(29.99, 29.98, false)]
    [InlineData(29.991, 29.99, true)] // sub-cent noise collapses to the same minor unit
    [InlineData(0.0, 0.0, true)]
    public void AreEqual_ComparesAtMinorUnitPrecision(double left, double right, bool expected)
    {
        Assert.Equal(expected, Money.AreEqual(left, right));
    }

    [Fact]
    public void AreEqual_Nullable_IsFalseWhenEitherOperandIsNull()
    {
        Assert.False(Money.AreEqual((double?)null, 10d));
        Assert.False(Money.AreEqual(10d, (double?)null));
        Assert.False(Money.AreEqual((double?)null, (double?)null));
        Assert.True(Money.AreEqual((double?)10d, (double?)10d));
    }

    [Theory]
    [InlineData(1.00, 0.50, true)]
    [InlineData(0.50, 0.50, false)]
    [InlineData(0.49, 0.50, false)]
    [InlineData(0.501, 0.50, false)] // still the same minor unit, so not strictly greater
    [InlineData(0.51, 0.50, true)]
    public void IsGreaterThan_ComparesAtMinorUnitPrecision(double amount, double threshold, bool expected)
    {
        Assert.Equal(expected, Money.IsGreaterThan(amount, threshold));
    }
}
