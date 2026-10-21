using YesSql.Indexes;

namespace CrestApps.OrchardCore.Addresses.Indexes;

/// <summary>
/// Indexes Country content items by their ISO code, for the country lists and the check that no two countries share a
/// code.
/// </summary>
public sealed class CountryIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the stable content item identifier of the indexed country.
    /// </summary>
    public string ContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the ISO 3166-1 alpha-2 code of the indexed country, in capitals.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Gets or sets the display name of the indexed country.
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
