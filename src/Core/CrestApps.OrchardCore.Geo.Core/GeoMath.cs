using CrestApps.OrchardCore.Geo.Models;

namespace CrestApps.OrchardCore.Geo.Core;

/// <summary>
/// Distances on the Earth.
/// </summary>
public static class GeoMath
{
    /// <summary>
    /// The mean radius of the Earth, in meters.
    /// </summary>
    public const double EarthRadiusMeters = 6_371_008.8;

    /// <summary>
    /// Gives the great-circle distance between two points (the haversine formula).
    /// </summary>
    /// <param name="from">The first point.</param>
    /// <param name="to">The second point.</param>
    /// <returns>The distance in meters.</returns>
    public static double DistanceMeters(GeoPoint from, GeoPoint to)
    {
        var lat1 = Radians(from.Latitude);
        var lat2 = Radians(to.Latitude);
        var dLat = lat2 - lat1;
        var dLon = Radians(to.Longitude - from.Longitude);
        var h = (Math.Sin(dLat / 2) * Math.Sin(dLat / 2)) + (Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2));

        return 2 * EarthRadiusMeters * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }

    private static double Radians(double degrees) => degrees * Math.PI / 180d;
}
