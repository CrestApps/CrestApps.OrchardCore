using System.Globalization;

namespace CrestApps.OrchardCore.Geo.Models;

/// <summary>
/// A point on the Earth, in WGS84 degrees.
/// </summary>
/// <param name="Latitude">The latitude, from -90 to 90.</param>
/// <param name="Longitude">The longitude, from -180 to 180.</param>
public readonly record struct GeoPoint(double Latitude, double Longitude)
{
    /// <summary>
    /// Gets a value indicating whether both coordinates are numbers in range.
    /// </summary>
    public bool IsValid => Latitude is >= -90 and <= 90 && Longitude is >= -180 and <= 180 && !double.IsNaN(Latitude) && !double.IsNaN(Longitude);

    /// <summary>
    /// Makes a point from nullable coordinates.
    /// </summary>
    /// <param name="latitude">The latitude.</param>
    /// <param name="longitude">The longitude.</param>
    /// <returns>The point, or <see langword="null"/> when a coordinate is missing or out of range.</returns>
    public static GeoPoint? From(double? latitude, double? longitude)
    {
        if (latitude is null || longitude is null)
        {
            return null;
        }

        var point = new GeoPoint(latitude.Value, longitude.Value);

        return point.IsValid ? point : null;
    }

    /// <inheritdoc/>
    public override string ToString()
        => string.Create(CultureInfo.InvariantCulture, $"{Latitude:0.######},{Longitude:0.######}");
}
