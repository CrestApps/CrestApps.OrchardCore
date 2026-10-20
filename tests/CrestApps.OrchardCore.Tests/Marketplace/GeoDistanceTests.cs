using CrestApps.OrchardCore.Classifieds.Services;

namespace CrestApps.OrchardCore.Tests.Marketplace;

/// <summary>
/// A search by distance narrows by latitude and longitude, then keeps what's within the distance on the earth.
/// </summary>
public sealed class GeoDistanceTests
{
    [Fact]
    public void Seattle_IsAbout10KilometersFromBellevue_And1000FromSanFranciscosSide()
    {
        Assert.InRange(GeoDistance.GetKilometers(47.6062, -122.3321, 47.6101, -122.2015), 9.5, 10.2);
        Assert.InRange(GeoDistance.GetKilometers(47.6062, -122.3321, 37.7749, -122.4194), 1090, 1100);
        Assert.Equal(0d, GeoDistance.GetKilometers(10, 10, 10, 10), 6);
    }

    [Fact]
    public void TheBounds_HoldEveryPointWithinTheDistance()
    {
        var (minLatitude, maxLatitude, minLongitude, maxLongitude) = GeoDistance.GetBounds(47.6062, -122.3321, 25);

        Assert.True(minLatitude < 47.6101 && 47.6101 < maxLatitude);
        Assert.True(minLongitude < -122.2015 && -122.2015 < maxLongitude);
        Assert.True(maxLatitude - minLatitude < 0.5);
    }

    [Theory]
    [InlineData(47.6, -122.3, true)]
    [InlineData(91, 0, false)]
    [InlineData(0, 181, false)]
    public void APlace_IsOnTheEarth(double latitude, double longitude, bool valid)
        => Assert.Equal(valid, GeoDistance.IsValid(latitude, longitude));
}
