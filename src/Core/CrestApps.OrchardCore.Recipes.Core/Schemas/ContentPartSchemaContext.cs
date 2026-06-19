using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Provides contextual information for building a content part payload schema.
/// </summary>
public sealed class ContentPartSchemaContext
{
    /// <summary>
    /// Gets the content type part definition being projected into the schema.
    /// </summary>
    public required ContentTypePartDefinition ContentTypePartDefinition { get; init; }
}
