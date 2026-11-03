namespace CrestApps.OrchardCore.Seo.Models;

/// <summary>
/// The settings of the Schema.org part on a content type: the schema.org type of its content items and where each of
/// their properties comes from.
/// </summary>
public sealed class SchemaOrgPartSettings
{
    /// <summary>
    /// Gets or sets the schema.org type, such as <c>BlogPosting</c> or <c>Event</c>. When it is empty, modules can
    /// choose a type (a product is a <c>Product</c>), and other content items are a <c>WebPage</c>.
    /// </summary>
    public string SchemaType { get; set; }

    /// <summary>
    /// Gets or sets the properties and where their values come from, in the order they are written.
    /// </summary>
    public List<SchemaOrgPropertyMapping> Properties { get; set; } = [];
}
