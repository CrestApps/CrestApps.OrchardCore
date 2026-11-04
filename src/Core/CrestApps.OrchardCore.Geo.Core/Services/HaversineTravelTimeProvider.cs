using CrestApps.OrchardCore.Geo.Models;
using CrestApps.OrchardCore.Geo.Services;

namespace CrestApps.OrchardCore.Geo.Core.Services;

/// <summary>
/// The built-in travel estimate: the straight-line distance times a road factor, at an average speed. It also draws
/// routes as straight lines.
/// </summary>
public sealed class HaversineTravelTimeProvider : ITravelTimeProvider, IRouteProvider
{
    private readonly IGeoSettingsService _settingsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="HaversineTravelTimeProvider"/> class.
    /// </summary>
    /// <param name="settingsService">The settings service.</param>
    public HaversineTravelTimeProvider(IGeoSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    /// <inheritdoc/>
    public string Name => GeoConstants.HaversineProviderName;

    /// <inheritdoc/>
    public async Task<TravelMatrix> GetMatrixAsync(IReadOnlyList<GeoPoint> origins, IReadOnlyList<GeoPoint> destinations, DateTime departureUtc, TravelOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(origins);
        ArgumentNullException.ThrowIfNull(destinations);

        var settings = await _settingsService.GetAsync();
        var speed = Speed(options?.SpeedKmh ?? settings.AverageSpeedKmh);
        var factor = Math.Max(1, settings.RoadFactor);

        return new TravelMatrix
        {
            Provider = Name,
            Legs = [.. origins.Select(origin => (IReadOnlyList<TravelLeg>)[.. destinations.Select(destination => Leg(origin, destination, factor, speed))])],
        };
    }

    /// <inheritdoc/>
    public async Task<GeoRoute> GetRouteAsync(IReadOnlyList<GeoPoint> points, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(points);

        var settings = await _settingsService.GetAsync();
        var speed = Speed(settings.AverageSpeedKmh);
        var factor = Math.Max(1, settings.RoadFactor);
        var legs = points.Zip(points.Skip(1), (from, to) => Leg(from, to, factor, speed)).ToList();

        return new GeoRoute
        {
            Provider = Name,
            Line = [.. points],
            DistanceMeters = legs.Sum(leg => leg.DistanceMeters),
            Duration = TimeSpan.FromSeconds(legs.Sum(leg => leg.Duration.TotalSeconds)),
        };
    }

    /// <summary>
    /// Estimates one leg.
    /// </summary>
    /// <param name="from">Where it starts.</param>
    /// <param name="to">Where it ends.</param>
    /// <param name="roadFactor">How much longer roads are than a straight line.</param>
    /// <param name="speedKmh">The average speed.</param>
    /// <returns>The leg.</returns>
    public static TravelLeg Leg(GeoPoint from, GeoPoint to, double roadFactor, double speedKmh)
    {
        var meters = GeoMath.DistanceMeters(from, to) * roadFactor;

        return new TravelLeg(TimeSpan.FromHours(meters / 1000d / Speed(speedKmh)), meters);
    }

    private static double Speed(double speedKmh) => speedKmh > 1 ? speedKmh : 1;
}
