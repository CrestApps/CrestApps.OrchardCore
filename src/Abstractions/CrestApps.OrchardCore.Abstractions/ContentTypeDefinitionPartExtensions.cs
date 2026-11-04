using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore;

/// <summary>
/// Reads the parts a content type carries.
/// </summary>
public static class ContentTypeDefinitionPartExtensions
{
    /// <summary>
    /// Determines whether the content type carries the named part, matched by the part definition's name or by the
    /// name the part has on the type.
    /// </summary>
    /// <param name="definition">The content type definition, which may be <see langword="null"/>.</param>
    /// <param name="partName">The technical name of the part.</param>
    /// <returns><see langword="true"/> when the type carries the part; otherwise, <see langword="false"/>.</returns>
    public static bool HasPart(this ContentTypeDefinition definition, string partName)
    {
        if (definition?.Parts is null || string.IsNullOrEmpty(partName))
        {
            return false;
        }

        foreach (var part in definition.Parts)
        {
            if (string.Equals(part.PartDefinition?.Name, partName, StringComparison.Ordinal) ||
                string.Equals(part.Name, partName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
