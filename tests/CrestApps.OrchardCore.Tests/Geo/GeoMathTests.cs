using CrestApps.OrchardCore.Geo.Core;
using CrestApps.OrchardCore.Geo.Models;

namespace CrestApps.OrchardCore.Tests.Geo;

/// <summary>
/// Distances, GeoJSON shapes and points inside them.
/// </summary>
public sealed class GeoMathTests
{
    // A square around downtown with a hole in its middle, and a second square to the east.
    private const string Shape = """
        {
          "type": "Feature",
          "geometry": {
            "type": "MultiPolygon",
            "coordinates": [
              [ [ [-71.10, 42.34], [-71.04, 42.34], [-71.04, 42.38], [-71.10, 42.38], [-71.10, 42.34] ],
                [ [-71.08, 42.355], [-71.06, 42.355], [-71.06, 42.365], [-71.08, 42.365], [-71.08, 42.355] ] ],
              [ [ [-70.90, 42.34], [-70.86, 42.34], [-70.86, 42.38], [-70.90, 42.38], [-70.90, 42.34] ] ]
            ]
          }
        }
        """;

    [Fact]
    public void TheHaversineDistance_MatchesAKnownDistance()
    {
        var london = new GeoPoint(51.5074, -0.1278);
        var paris = new GeoPoint(48.8566, 2.3522);

        // London to Paris is about 343.5 km as the crow flies.
        Assert.InRange(GeoMath.DistanceMeters(london, paris), 342_500, 344_500);
        Assert.Equal(0, GeoMath.DistanceMeters(london, london), 6);
    }

    [Fact]
    public void AShape_HasItsHolesAndAllItsPolygons()
    {
        Assert.True(GeoShape.TryParse(Shape, out var shape, out var error), error);

        Assert.True(shape.Contains(new GeoPoint(42.345, -71.09)));
        Assert.False(shape.Contains(new GeoPoint(42.36, -71.07)));
        Assert.True(shape.Contains(new GeoPoint(42.36, -70.88)));
        Assert.False(shape.Contains(new GeoPoint(42.36, -70.95)));
        Assert.Equal(2, shape.Polygons.Count);
    }

    [Theory]
    [InlineData("", "The shape is empty.")]
    [InlineData("{ \"type\": \"Point\", \"coordinates\": [0, 0] }", "The shape has no polygon.")]
    [InlineData("{ \"type\": \"Polygon\" }", "The shape isn't valid GeoJSON: a part is missing or has the wrong type.")]
    [InlineData("{ \"type\": \"Polygon\", \"coordinates\": [ [ [\"a\", 0], [1, 1], [2, 0] ] ] }", "The shape isn't valid GeoJSON: a part is missing or has the wrong type.")]
    [InlineData("{ \"type\": \"Polygon\", \"coordinates\": [ [ [0, 0], [1, 1] ] ] }", "A polygon needs at least three corners.")]
    [InlineData("{ \"type\": \"Polygon\", \"coordinates\": [ [ [0, 0], [1, 95], [2, 0] ] ] }", "The position 1, 95 is out of range.")]
    public void ABadShape_SaysWhy(string geoJson, string expected)
    {
        Assert.False(GeoShape.TryParse(geoJson, out _, out var error));
        Assert.Equal(expected, error);
    }

    [Fact]
    public void BrokenJson_IsReported()
    {
        Assert.False(GeoShape.TryParse("{ not json", out _, out var error));
        Assert.StartsWith("The shape isn't valid GeoJSON", error);
    }
}
