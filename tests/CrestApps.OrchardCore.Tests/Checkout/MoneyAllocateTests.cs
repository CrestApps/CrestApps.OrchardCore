using CrestApps.OrchardCore.Payments;

namespace CrestApps.OrchardCore.Tests.Checkout;

public sealed class MoneyAllocateTests
{
    [Fact]
    public void Allocate_WhenTheSplitIsNotExact_GivesTheRemainderToTheLastShare()
    {
        // Act
        var shares = Money.Allocate(100m, [1m, 1m, 1m], "USD");

        // Assert
        Assert.Equal([33.33m, 33.33m, 33.34m], shares);
        Assert.Equal(100m, shares.Sum());
    }

    [Fact]
    public void Allocate_ByPercent_FollowsTheWeights()
    {
        // Act
        var shares = Money.Allocate(1250.75m, [60m, 25m, 15m], "USD");

        // Assert
        Assert.Equal([750.45m, 312.69m, 187.61m], shares);
        Assert.Equal(1250.75m, shares.Sum());
    }

    [Fact]
    public void Allocate_ForACurrencyWithoutDecimals_SplitsWholeUnits()
    {
        // Act
        var shares = Money.Allocate(1000m, [1m, 2m], "JPY");

        // Assert
        Assert.Equal([333m, 667m], shares);
    }

    [Fact]
    public void Allocate_ForAThreeDecimalCurrency_KeepsThousandths()
    {
        // Act
        var shares = Money.Allocate(1m, [1m, 1m, 1m], "KWD");

        // Assert
        Assert.Equal([0.333m, 0.333m, 0.334m], shares);
    }

    [Fact]
    public void Allocate_WhenAWeightIsZero_GivesThatShareNothing_AndTheRemainderToTheLastWeightedShare()
    {
        // Act
        var shares = Money.Allocate(10m, [1m, 2m, 0m], "USD");

        // Assert
        Assert.Equal([3.33m, 6.67m, 0m], shares);
    }

    [Fact]
    public void Allocate_ANegativeTotal_SplitsItTheSameWay()
    {
        // Act
        var shares = Money.Allocate(-100m, [1m, 1m, 1m], "USD");

        // Assert
        Assert.Equal([-33.33m, -33.33m, -33.34m], shares);
    }

    [Fact]
    public void Allocate_WhenRoundingUpWouldPassTheTotal_NeverGivesANegativeShare()
    {
        // Arrange: three shares of half a cent each round up to a cent, more than the two cents there are.
        var weights = new[] { 1m, 1m, 1m, 1m };

        // Act
        var shares = Money.Allocate(0.02m, weights, "USD");

        // Assert
        Assert.All(shares, share => Assert.True(share >= 0m));
        Assert.Equal(0.02m, shares.Sum());
    }

    [Fact]
    public void Allocate_AZeroTotal_GivesEveryShareZero()
    {
        // Act
        var shares = Money.Allocate(0m, [0m, 0m], "USD");

        // Assert
        Assert.Equal([0m, 0m], shares);
    }

    [Fact]
    public void Allocate_WhenNoShareHasAWeight_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => Money.Allocate(10m, [0m, 0m], "USD"));
    }

    [Fact]
    public void Allocate_WhenAWeightIsNegative_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => Money.Allocate(10m, [1m, -1m], "USD"));
    }
}
