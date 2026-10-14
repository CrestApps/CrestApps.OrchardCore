namespace CrestApps.OrchardCore.Products.Core.Listing;

/// <summary>
/// One value of one product attribute, such as <c>Year = 2016</c> or <c>Make = Toyota</c>.
/// </summary>
/// <remarks>
/// A field with several values (tags, a multi-select, a taxonomy term with its ancestors) produces one entry
/// per value, which is what lets a filter match any of them and a facet count each one.
/// </remarks>
public sealed class ProductAttributeValue
{
    /// <summary>
    /// Gets or sets the attribute key: the field name, or a reserved key from <see cref="ProductAttributeKeys"/>.
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Gets or sets the canonical text value filters match on: the text itself, a term or item identifier,
    /// or <c>true</c>/<c>false</c>.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets the numeric value, for numeric attributes.
    /// </summary>
    public decimal? Number { get; set; }

    /// <summary>
    /// Gets or sets the date value, for date attributes.
    /// </summary>
    public DateTime? Date { get; set; }

    /// <summary>
    /// Gets or sets the text shown to visitors, such as a term's name for its identifier.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the depth of a taxonomy term (1 for a top-level term), or 0 for values that are not terms.
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// Gets or sets the value's position among its siblings, so options can be listed in their defined order.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Gets or sets whether keyword search matches this value.
    /// </summary>
    public bool Searchable { get; set; }
}
