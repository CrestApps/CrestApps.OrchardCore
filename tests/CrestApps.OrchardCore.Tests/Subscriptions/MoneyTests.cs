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
        Assert.Equal((decimal)expected, Money.Round((decimal)amount));
    }

    [Theory]
    [InlineData(29.99, 2999)]
    [InlineData(0.5, 50)]
    [InlineData(10, 1000)]
    [InlineData(0.005, 1)]
    public void ToMinorUnits_ConvertsMajorUnitsToWholeMinorUnits(double amount, long expected)
    {
        Assert.Equal(expected, Money.ToMinorUnits((decimal)amount));
    }

    [Fact]
    public void AreEqual_TreatsSubCentDifferencesAsEqual()
    {
        var summed = 19.99m + 10.00m;

        Assert.True(Money.AreEqual(summed, 29.99m));
    }

    [Theory]
    [InlineData(29.99, 29.99, true)]
    [InlineData(29.99, 29.98, false)]
    [InlineData(29.991, 29.99, true)] // sub-cent noise collapses to the same minor unit
    [InlineData(0.0, 0.0, true)]
    public void AreEqual_ComparesAtMinorUnitPrecision(double left, double right, bool expected)
    {
        Assert.Equal(expected, Money.AreEqual((decimal)left, (decimal)right));
    }

    [Fact]
    public void AreEqual_Nullable_IsFalseWhenEitherOperandIsNull()
    {
        Assert.False(Money.AreEqual((decimal?)null, 10m));
        Assert.False(Money.AreEqual(10m, (decimal?)null));
        Assert.False(Money.AreEqual((decimal?)null, (decimal?)null));
        Assert.True(Money.AreEqual((decimal?)10m, (decimal?)10m));
    }

    [Theory]
    [InlineData(1.00, 0.50, true)]
    [InlineData(0.50, 0.50, false)]
    [InlineData(0.49, 0.50, false)]
    [InlineData(0.501, 0.50, false)] // still the same minor unit, so not strictly greater
    [InlineData(0.51, 0.50, true)]
    public void IsGreaterThan_ComparesAtMinorUnitPrecision(double amount, double threshold, bool expected)
    {
        Assert.Equal(expected, Money.IsGreaterThan((decimal)amount, (decimal)threshold));
    }
}
