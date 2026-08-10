using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Sitemaps;

/// <summary>
/// Describes a sitemap source discovered from the registered
/// <see cref="ISitemapSourceSchemaDefinition"/> contributions.
/// </summary>
public sealed class SitemapSourceDescriptor
{
    /// <summary>
    /// Gets or sets the sitemap source name, for example <c>ContentTypesSitemapSource</c>.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the polymorphic type discriminator serialized as the <c>$type</c> member.
    /// </summary>
    public string TypeDiscriminator { get; set; }

    /// <summary>
    /// Gets or sets the human readable source title shown in the sitemap editor.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets a description explaining what the source contributes to the sitemap.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the property definitions specific to the source, beyond the shared members.
    /// </summary>
    public IReadOnlyList<(string Name, JsonSchemaBuilder Schema)> Properties { get; set; } = [];

    /// <summary>
    /// Gets or sets the names of the properties that must be provided in addition to the shared members.
    /// </summary>
    public IReadOnlyList<string> RequiredProperties { get; set; } = [];
}
