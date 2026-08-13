using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Sitemaps;

/// <summary>
/// Describes the recipe payload of a single sitemap source inside a sitemap's <c>SitemapSources</c> array.
/// </summary>
public sealed class SitemapSourceSchema
{
    /// <summary>
    /// Gets or sets the human readable source title shown in the sitemap editor.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets a description explaining what the source contributes to the sitemap.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the property definitions that are specific to the source, beyond the shared
    /// <c>$type</c> and <c>Id</c> members.
    /// </summary>
    public IReadOnlyList<(string Name, JsonSchemaBuilder Schema)> Properties { get; set; } = [];

    /// <summary>
    /// Gets or sets the names of the properties that must be provided in addition to the shared members.
    /// </summary>
    public IReadOnlyList<string> RequiredProperties { get; set; } = [];
}
