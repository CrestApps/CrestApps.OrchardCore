namespace CrestApps.OrchardCore.Classifieds.Services;

/// <summary>
/// Distances on the earth's surface, for a search by distance.
/// </summary>
public static class GeoDistance
{
    private const double EarthRadiusKm = 6371.0088;

    /// <summary>
    /// Returns the great-circle distance between two points, in kilometers.
    /// </summary>
    public static double GetKilometers(double latitude1, double longitude1, double latitude2, double longitude2)
    {
        var dLat = ToRadians(latitude2 - latitude1);
        var dLon = ToRadians(longitude2 - longitude1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
            Math.Cos(ToRadians(latitude1)) * Math.Cos(ToRadians(latitude2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        return EarthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    /// <summary>
    /// Returns the latitudes and longitudes that hold every point within <paramref name="kilometers"/> of a center, to
    /// narrow a query before the exact distance is worked out.
    /// </summary>
    public static (double MinLatitude, double MaxLatitude, double MinLongitude, double MaxLongitude) GetBounds(double latitude, double longitude, double kilometers)
    {
        var latitudeDelta = kilometers / 111.32;
        var cos = Math.Cos(ToRadians(latitude));
        var longitudeDelta = cos < 0.0001 ? 180d : kilometers / (111.32 * cos);

        return (
            Math.Max(-90d, latitude - latitudeDelta),
            Math.Min(90d, latitude + latitudeDelta),
            Math.Max(-180d, longitude - longitudeDelta),
            Math.Min(180d, longitude + longitudeDelta));
    }

    /// <summary>
    /// Returns whether a latitude and a longitude are on the earth.
    /// </summary>
    public static bool IsValid(double? latitude, double? longitude)
        => latitude is >= -90d and <= 90d && longitude is >= -180d and <= 180d;

    private static double ToRadians(double degrees)
        => degrees * Math.PI / 180d;
}
