using CrestApps.OrchardCore.Geo.Core.Services;
using CrestApps.OrchardCore.Geo.Models;

namespace CrestApps.OrchardCore.Tests.Geo;

/// <summary>
/// Territories are found by the smallest shape a point is in, else by postal code, exact before the longest prefix.
/// </summary>
public sealed class TerritoryMatcherTests
{
    private static readonly ServiceTerritory _metro = Territory("Metro", Square(-71.20, 42.20, -70.90, 42.50), "02*");
    private static readonly ServiceTerritory _downtown = Territory("Downtown", Square(-71.08, 42.34, -71.04, 42.37));
    private static readonly ServiceTerritory _south = Territory("South", null, "021*", "02186");
    private static readonly ServiceTerritory _island = Territory("Island", null, "02554");
    private static readonly ServiceTerritory[] _all = [_metro, _downtown, _south, _island];

    [Fact]
    public void ThePoint_FindsTheSmallestShapeItIsIn()
    {
        var match = TerritoryMatcher.Match(_all, new GeoPoint(42.355, -71.06), "02108");

        Assert.Same(_downtown, match.Territory);
        Assert.Equal(TerritoryMatchKind.Shape, match.MatchedBy);
        Assert.Same(_metro, TerritoryMatcher.Match(_all, new GeoPoint(42.45, -71.10), null).Territory);
    }

    [Fact]
    public void WithoutAShape_ThePostalCodeDecides_ExactBeforeTheLongestPrefix()
    {
        Assert.Same(_south, TerritoryMatcher.Match(_all, null, "02186").Territory);
        Assert.Same(_south, TerritoryMatcher.Match(_all, null, "02 140").Territory);
        Assert.Same(_metro, TerritoryMatcher.Match(_all, null, "02554-1234").Territory);
        Assert.Same(_island, TerritoryMatcher.Match(_all, new GeoPoint(41.28, -70.10), "02554").Territory);
        Assert.Equal(TerritoryMatchKind.PostalCode, TerritoryMatcher.Match(_all, null, "02186").MatchedBy);
        Assert.Null(TerritoryMatcher.Match(_all, new GeoPoint(40.7, -74.0), "10001"));
    }

    private static ServiceTerritory Territory(string name, string shape, params string[] postalCodes)
        => new() { ItemId = name.ToLowerInvariant(), Name = name, Shape = shape, PostalCodes = [.. postalCodes.Select(code => code.EndsWith('*') ? code : TerritoryMatcher.NormalizePostalCode(code))] };

    private static string Square(double west, double south, double east, double north)
        => System.FormattableString.Invariant($"{{ \"type\": \"Polygon\", \"coordinates\": [ [ [{west}, {south}], [{east}, {south}], [{east}, {north}], [{west}, {north}], [{west}, {south}] ] ] }}");
}
