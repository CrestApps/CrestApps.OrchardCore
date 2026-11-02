using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Geo.Models;

/// <summary>
/// An area served together: drawn as a shape on the map, or listed as postal codes, or both.
/// </summary>
public sealed class ServiceTerritory : CatalogItem
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the territory it is part of, or <see langword="null"/>.
    /// </summary>
    public string ParentId { get; set; }

    /// <summary>
    /// Gets or sets the shape, as GeoJSON (a Polygon or MultiPolygon geometry, Feature or FeatureCollection).
    /// </summary>
    public string Shape { get; set; }

    /// <summary>
    /// Gets or sets the postal codes; one ending with <c>*</c> matches every code that starts with the rest.
    /// </summary>
    public List<string> PostalCodes { get; set; } = [];

    /// <summary>
    /// Gets or sets the time zone, or <see langword="null"/> for the site's.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets the average travel speed in km/h, or <see langword="null"/> for the site's.
    /// </summary>
    public double? TravelSpeedKmh { get; set; }

    /// <summary>
    /// Gets or sets the people and teams that serve it, by the identifiers of the modules that own them.
    /// </summary>
    public List<TerritoryMember> Members { get; set; } = [];

    /// <summary>
    /// Gets or sets when it was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when it was last changed.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}

/// <summary>
/// A team or person that serves a territory.
/// </summary>
public sealed class TerritoryMember
{
    /// <summary>
    /// Gets or sets the kind of member, such as <c>Team</c> or <c>Worker</c>.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets the member's identifier.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets whether the territory is theirs first or as a backup.
    /// </summary>
    public TerritoryRole Role { get; set; }
}

/// <summary>
/// How a member serves a territory.
/// </summary>
public enum TerritoryRole
{
    /// <summary>
    /// First.
    /// </summary>
    Primary,

    /// <summary>
    /// As a backup.
    /// </summary>
    Secondary,
}

/// <summary>
/// How a point or address was matched to a territory.
/// </summary>
public enum TerritoryMatchKind
{
    /// <summary>
    /// The point is inside the territory's shape.
    /// </summary>
    Shape,

    /// <summary>
    /// The postal code is one of the territory's.
    /// </summary>
    PostalCode,
}

/// <summary>
/// A territory found for a point or postal code.
/// </summary>
/// <param name="Territory">The territory.</param>
/// <param name="MatchedBy">How it was matched.</param>
public sealed record TerritoryMatch(ServiceTerritory Territory, TerritoryMatchKind MatchedBy);
