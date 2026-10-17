using YesSql.Indexes;

namespace CrestApps.OrchardCore.Addresses.Indexes;

/// <summary>
/// Indexes Country content items by their ISO code, for the country lists and the check that no two countries share a
/// code. The table is named for the wider set of places it once held.
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
    /// Gets or sets the parent place. Countries have none; the column is kept for the rows of sites that indexed
    /// regions before.
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
