using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Seo.ViewModels;

/// <summary>
/// The editor of the Schema.org part's settings on a content type.
/// </summary>
public class SchemaOrgPartSettingsViewModel
{
    /// <summary>
    /// Gets or sets the schema.org type.
    /// </summary>
    public string SchemaType { get; set; }

    /// <summary>
    /// Gets or sets the property mappings, as the JSON the editor's script writes.
    /// </summary>
    public string Properties { get; set; }

    /// <summary>
    /// Gets or sets what the editor's script needs, as JSON: the types and their properties, and the values the
    /// content type's items can give.
    /// </summary>
    [BindNever]
    public string EditorData { get; set; }
}
