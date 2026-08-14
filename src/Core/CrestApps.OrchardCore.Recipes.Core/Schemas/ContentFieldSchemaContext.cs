using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Provides contextual information for building a content field payload schema.
/// </summary>
public sealed class ContentFieldSchemaContext
{
    /// <summary>
    /// Gets the content part field definition being projected into the schema.
    /// </summary>
    public required ContentPartFieldDefinition ContentPartFieldDefinition { get; init; }

    /// <summary>
    /// Gets the content type part definition that owns the field.
    /// </summary>
    public required ContentTypePartDefinition ContentTypePartDefinition { get; init; }
}
