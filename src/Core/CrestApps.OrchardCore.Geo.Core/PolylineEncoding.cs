using CrestApps.OrchardCore.Geo.Models;

namespace CrestApps.OrchardCore.Geo.Core;

/// <summary>
/// Reads the encoded polyline format several route providers use.
/// </summary>
public static class PolylineEncoding
{
    /// <summary>
    /// Decodes an encoded polyline.
    /// </summary>
    /// <param name="encoded">The encoded line.</param>
    /// <param name="precision">The decimal places it was encoded with: 5 for most providers, 6 for some.</param>
    /// <returns>The points.</returns>
    public static IReadOnlyList<GeoPoint> Decode(string encoded, int precision = 5)
    {
        var points = new List<GeoPoint>();

        if (string.IsNullOrEmpty(encoded))
        {
            return points;
        }

        var factor = Math.Pow(10, precision);
        var index = 0;
        var latitude = 0;
        var longitude = 0;

        while (index < encoded.Length)
        {
            latitude += Next(encoded, ref index);
            longitude += Next(encoded, ref index);
            points.Add(new GeoPoint(latitude / factor, longitude / factor));
        }

        return points;
    }

    private static int Next(string encoded, ref int index)
    {
        var result = 0;
        var shift = 0;
        int chunk;

        do
        {
            if (index >= encoded.Length)
            {
                throw new FormatException("The encoded line ends in the middle of a number.");
            }

            chunk = encoded[index++] - 63;
            result |= (chunk & 0x1F) << shift;
            shift += 5;
        }
        while (chunk >= 0x20);

        return (result & 1) != 0 ? ~(result >> 1) : result >> 1;
    }
}
