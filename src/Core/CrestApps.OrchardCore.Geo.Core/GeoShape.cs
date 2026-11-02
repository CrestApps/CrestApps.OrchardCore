using System.Text.Json;
using CrestApps.OrchardCore.Geo.Models;

namespace CrestApps.OrchardCore.Geo.Core;

/// <summary>
/// The polygons of a GeoJSON shape, for testing whether a point is inside.
/// </summary>
public sealed class GeoShape
{
    private GeoShape(IReadOnlyList<GeoPolygon> polygons)
    {
        Polygons = polygons;
    }

    /// <summary>
    /// Gets the polygons.
    /// </summary>
    public IReadOnlyList<GeoPolygon> Polygons { get; }

    /// <summary>
    /// Gets the shape's area in square degrees, to tell a smaller shape from a larger one.
    /// </summary>
    public double Area => Polygons.Sum(polygon => polygon.Area);

    /// <summary>
    /// Tells whether a point is inside the shape (on the outer ring and outside every hole).
    /// </summary>
    /// <param name="point">The point.</param>
    /// <returns><see langword="true"/> when it is inside.</returns>
    public bool Contains(GeoPoint point) => Polygons.Any(polygon => polygon.Contains(point));

    /// <summary>
    /// Reads a Polygon or MultiPolygon from GeoJSON: a geometry, a Feature or a FeatureCollection.
    /// </summary>
    /// <param name="geoJson">The GeoJSON.</param>
    /// <param name="shape">The shape.</param>
    /// <param name="error">Why it can't be read.</param>
    /// <returns><see langword="true"/> when it was read.</returns>
    public static bool TryParse(string geoJson, out GeoShape shape, out string error)
    {
        shape = null;
        error = null;

        if (string.IsNullOrWhiteSpace(geoJson))
        {
            error = "The shape is empty.";

            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(geoJson);
            var polygons = new List<GeoPolygon>();

            Collect(document.RootElement, polygons);

            if (polygons.Count == 0)
            {
                error = "The shape has no polygon.";

                return false;
            }

            shape = new GeoShape(polygons);

            return true;
        }
        catch (JsonException ex)
        {
            error = $"The shape isn't valid GeoJSON: {ex.Message}";
        }
        catch (FormatException ex)
        {
            error = ex.Message;
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
        {
            // A member is missing or holds the wrong kind of value, such as a polygon without coordinates.
            error = "The shape isn't valid GeoJSON: a part is missing or has the wrong type.";
        }

        return false;
    }

    private static void Collect(JsonElement element, List<GeoPolygon> polygons)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty("type", out var typeElement))
        {
            throw new FormatException("A GeoJSON object needs a type.");
        }

        switch (typeElement.GetString())
        {
            case "FeatureCollection":
                foreach (var feature in element.GetProperty("features").EnumerateArray())
                {
                    Collect(feature, polygons);
                }

                break;
            case "Feature":
                if (element.TryGetProperty("geometry", out var geometry) && geometry.ValueKind == JsonValueKind.Object)
                {
                    Collect(geometry, polygons);
                }

                break;
            case "GeometryCollection":
                foreach (var item in element.GetProperty("geometries").EnumerateArray())
                {
                    Collect(item, polygons);
                }

                break;
            case "Polygon":
                polygons.Add(ReadPolygon(element.GetProperty("coordinates")));
                break;
            case "MultiPolygon":
                foreach (var polygon in element.GetProperty("coordinates").EnumerateArray())
                {
                    polygons.Add(ReadPolygon(polygon));
                }

                break;
            default:
                // Points and lines have no inside; they are left out.
                break;
        }
    }

    private static GeoPolygon ReadPolygon(JsonElement rings)
    {
        var read = rings.EnumerateArray().Select(ReadRing).ToList();

        if (read.Count == 0 || read[0].Count < 3)
        {
            throw new FormatException("A polygon needs at least three corners.");
        }

        return new GeoPolygon(read[0], read.Skip(1).ToList());
    }

    private static List<GeoPoint> ReadRing(JsonElement ring)
    {
        // GeoJSON positions are [longitude, latitude].
        var points = ring.EnumerateArray().Select(position =>
        {
            var lon = position[0].GetDouble();
            var lat = position[1].GetDouble();
            var point = new GeoPoint(lat, lon);

            return point.IsValid ? point : throw new FormatException($"The position {lon}, {lat} is out of range.");
        }).ToList();

        if (points.Count > 1 && points[0] == points[^1])
        {
            points.RemoveAt(points.Count - 1);
        }

        return points;
    }
}

/// <summary>
/// A polygon with its holes.
/// </summary>
public sealed class GeoPolygon
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GeoPolygon"/> class.
    /// </summary>
    /// <param name="outer">The outer ring.</param>
    /// <param name="holes">The holes.</param>
    public GeoPolygon(IReadOnlyList<GeoPoint> outer, IReadOnlyList<IReadOnlyList<GeoPoint>> holes)
    {
        Outer = outer;
        Holes = holes;
    }

    /// <summary>
    /// Gets the outer ring.
    /// </summary>
    public IReadOnlyList<GeoPoint> Outer { get; }

    /// <summary>
    /// Gets the holes.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<GeoPoint>> Holes { get; }

    /// <summary>
    /// Gets the area in square degrees, less the holes.
    /// </summary>
    public double Area => RingArea(Outer) - Holes.Sum(RingArea);

    /// <summary>
    /// Tells whether a point is inside the outer ring and outside every hole.
    /// </summary>
    /// <param name="point">The point.</param>
    /// <returns><see langword="true"/> when it is inside.</returns>
    public bool Contains(GeoPoint point) => InRing(Outer, point) && !Holes.Any(hole => InRing(hole, point));

    // Even-odd ray casting on longitude and latitude, fine for territories that don't cross the antimeridian.
    private static bool InRing(IReadOnlyList<GeoPoint> ring, GeoPoint point)
    {
        var inside = false;

        for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
        {
            var a = ring[i];
            var b = ring[j];

            if ((a.Latitude > point.Latitude) != (b.Latitude > point.Latitude) &&
                point.Longitude < ((b.Longitude - a.Longitude) * (point.Latitude - a.Latitude) / (b.Latitude - a.Latitude)) + a.Longitude)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static double RingArea(IReadOnlyList<GeoPoint> ring)
    {
        var sum = 0d;

        for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
        {
            sum += (ring[j].Longitude * ring[i].Latitude) - (ring[i].Longitude * ring[j].Latitude);
        }

        return Math.Abs(sum) / 2d;
    }
}
