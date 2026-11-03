using CrestApps.OrchardCore.Seo.SchemaOrg;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Seo.ViewModels;

/// <summary>
/// The editor of the Schema.org part on a content item.
/// </summary>
public class SchemaOrgPartViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether the content item's page publishes no structured data about it.
    /// </summary>
    public bool Disabled { get; set; }

    /// <summary>
    /// Gets or sets the values editors enter, by property name.
    /// </summary>
    public Dictionary<string, string> Values { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the properties whose values editors enter.
    /// </summary>
    [BindNever]
    public IList<SchemaOrgEditorField> Fields { get; set; } = [];

    /// <summary>
    /// Gets or sets the schema.org type of the content item.
    /// </summary>
    [BindNever]
    public string SchemaType { get; set; }

    /// <summary>
    /// Gets or sets the JSON-LD the content item's page publishes, as it was last saved.
    /// </summary>
    [BindNever]
    public string Preview { get; set; }
}

/// <summary>
/// A property whose value editors enter on each content item.
/// </summary>
public sealed class SchemaOrgEditorField
{
    /// <summary>
    /// Gets or sets the property name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the label of the input.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the description of the property.
    /// </summary>
    public string Hint { get; set; }

    /// <summary>
    /// Gets or sets the kind of value, which decides the input.
    /// </summary>
    public SchemaOrgValueKind Kind { get; set; }

    /// <summary>
    /// Gets or sets how much search engines need the property.
    /// </summary>
    public SchemaOrgPropertyLevel Level { get; set; }

    /// <summary>
    /// Gets or sets the values an enumeration accepts.
    /// </summary>
    public IList<SchemaOrgOption> Options { get; set; } = [];
}
