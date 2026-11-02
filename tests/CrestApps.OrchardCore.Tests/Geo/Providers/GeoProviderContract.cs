using CrestApps.OrchardCore.Geo.Models;
using CrestApps.OrchardCore.Geo.Services;

namespace CrestApps.OrchardCore.Tests.Geo.Providers;

/// <summary>
/// What every provider must do, checked against its recorded answers.
/// </summary>
internal static class GeoProviderContract
{
    public static readonly GeoPoint CityHall = new(42.3603, -71.0580);

    public static readonly GeoPoint Cambridge = new(42.3736, -71.1097);

    public static async Task GeocodesAsync(IGeocodingProvider provider)
    {
        var candidates = await provider.GeocodeAsync(new GeocodeQuery { Text = "1 City Hall Square, Boston" }, TestContext.Current.CancellationToken);

        Assert.NotEmpty(candidates);
        Assert.All(candidates, candidate =>
        {
            Assert.True(candidate.Point.IsValid);
            Assert.InRange(candidate.Confidence, 0, 1);
        });

        var best = candidates.OrderByDescending(candidate => candidate.Confidence).First();

        Assert.Equal(42.3603, best.Point.Latitude, 3);
        Assert.Equal(-71.058, best.Point.Longitude, 3);
        Assert.Equal("02201", best.PostalCode);
        Assert.Equal("US", best.Country);
        Assert.False(string.IsNullOrEmpty(best.FormattedAddress));
    }

    public static async Task FindsNothingAsync(IGeocodingProvider provider)
        => Assert.Empty(await provider.GeocodeAsync(new GeocodeQuery { Text = "Nowhere at all" }, TestContext.Current.CancellationToken));

    public static async Task ReportsARefusalAsync(Func<Task> call, string expected)
    {
        var error = await Assert.ThrowsAsync<GeoProviderException>(call);

        Assert.Contains(expected, error.Message, StringComparison.Ordinal);
    }

    public static async Task GivesAMatrixAsync(ITravelTimeProvider provider)
    {
        var matrix = await provider.GetMatrixAsync([CityHall], [CityHall, Cambridge], DateTime.UtcNow.AddHours(-1), new TravelOptions(), TestContext.Current.CancellationToken);

        Assert.Equal(provider.Name, matrix.Provider);
        Assert.Single(matrix.Legs);
        Assert.Equal(2, matrix.Legs[0].Count);
        Assert.Equal(TimeSpan.Zero, matrix[0, 0].Duration);
        Assert.Equal(840, matrix[0, 1].Duration.TotalSeconds, 0);
        Assert.Equal(6012, matrix[0, 1].DistanceMeters, 0);
    }

    public static async Task GivesARouteAsync(IRouteProvider provider)
    {
        var route = await provider.GetRouteAsync([CityHall, Cambridge], TestContext.Current.CancellationToken);

        Assert.Equal(provider.Name, route.Provider);
        Assert.True(route.Line.Count >= 2);
        Assert.All(route.Line, point => Assert.True(point.IsValid));
        Assert.Equal(6012, route.DistanceMeters, 0);
        Assert.Equal(840, route.Duration.TotalSeconds, 0);
        Assert.NotEmpty(route.Instructions);
    }
}
