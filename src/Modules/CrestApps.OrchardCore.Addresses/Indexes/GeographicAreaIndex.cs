using YesSql.Indexes;

namespace CrestApps.OrchardCore.Addresses.Indexes;

/// <summary>
/// Indexes every geographic content item (country, region, county, city, and district) by its content type,
/// money-safe code, and parent reference. A single shared index powers country selectors, code uniqueness,
/// cascading parent/child lookups, and address resolution across the whole hierarchy.
/// </summary>
public sealed class GeographicAreaIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the stable content item identifier of the indexed geographic area.
    /// </summary>
    public string ContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the content type of the indexed geographic area, for example <c>Country</c> or <c>City</c>.
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// Gets or sets the money-safe code of the indexed geographic area (for example the ISO code of a country).
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Gets or sets the content item identifier of the parent geographic area, or <see langword="null"/> for a
    /// country.
    /// </summary>
    public string ParentContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the display name of the indexed geographic area.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the indexed content item version is published.
    /// </summary>
    public bool Published { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the indexed content item version is the latest version.
    /// </summary>
    public bool Latest { get; set; }
}
