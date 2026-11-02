namespace CrestApps.OrchardCore.Seo.ViewModels;

/// <summary>
/// The editor of a content type's structured data settings.
/// </summary>
public class StructuredDataTypeSettingsViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether the content items of the type publish no structured data.
    /// </summary>
    public bool Disabled { get; set; }

    /// <summary>
    /// Gets or sets the schema.org type of the content items.
    /// </summary>
    public string SchemaType { get; set; }

    /// <summary>
    /// Gets or sets the schema.org property map, one <c>property: Liquid</c> per line.
    /// </summary>
    public string Properties { get; set; }
}
