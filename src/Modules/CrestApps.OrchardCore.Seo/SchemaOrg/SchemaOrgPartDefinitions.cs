using CrestApps.OrchardCore.Seo.Models;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Seo.SchemaOrg;

/// <summary>
/// Finds the Schema.org part of a content type.
/// </summary>
internal static class SchemaOrgPartDefinitions
{
    /// <summary>
    /// Finds the Schema.org part of a content type.
    /// </summary>
    /// <param name="definition">The content type definition.</param>
    /// <returns>The part, or <see langword="null"/> when the content type does not have it.</returns>
    public static ContentTypePartDefinition Find(ContentTypeDefinition definition)
        => definition?.Parts.FirstOrDefault(part => part.PartDefinition.Name == nameof(SchemaOrgPart));
}
