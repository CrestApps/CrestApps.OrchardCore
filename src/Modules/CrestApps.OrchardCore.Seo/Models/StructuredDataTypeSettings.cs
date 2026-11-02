namespace CrestApps.OrchardCore.Seo.Models;

/// <summary>
/// The structured data settings of a content type.
/// </summary>
public sealed class StructuredDataTypeSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether the content items of the type publish no structured data.
    /// </summary>
    public bool Disabled { get; set; }

    /// <summary>
    /// Gets or sets the schema.org type of the content items, such as <c>Article</c> or <c>Event</c>. When it is
    /// empty, modules can choose a type (a product is a <c>Product</c>), and other content items with a URL are a
    /// <c>WebPage</c>.
    /// </summary>
    public string SchemaType { get; set; }

    /// <summary>
    /// Gets or sets the schema.org properties of the content items, one per line, as <c>property: Liquid</c>, such
    /// as <c>headline: {{ ContentItem.DisplayText }}</c>. A dotted name, such as <c>offers.price</c>, sets a nested
    /// property.
    /// </summary>
    public string Properties { get; set; }
}
