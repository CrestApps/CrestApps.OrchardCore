using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Seo.Models;

/// <summary>
/// Publishes a content item as schema.org structured data (JSON-LD). The part's settings on the content type choose the
/// schema.org type and where each property's value comes from; editors fill in the values the settings leave to them.
/// </summary>
public sealed class SchemaOrgPart : ContentPart
{
    /// <summary>
    /// Gets or sets a value indicating whether this content item's page publishes no structured data about it.
    /// </summary>
    public bool Disabled { get; set; }

    /// <summary>
    /// Gets or sets the values editors entered, by property name, for the properties whose value the settings leave to
    /// them.
    /// </summary>
    public Dictionary<string, string> Values { get; set; } = new(StringComparer.Ordinal);
}
