using CrestApps.OrchardCore.Recipes.Core.Schemas;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Parts;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Recipes.Core;

/// <summary>
/// Provides nested content-type constraints for parts that contain child content items.
/// </summary>
/// <remarks>
/// Implement this interface alongside <see cref="PartSchemaDefinitionBase"/> for container-style
/// parts such as <c>BagPart</c> or <c>FlowPart</c>. The implementing class declares which payload
/// property holds nested items and which content types are allowed for the current attachment.
/// <see cref="Services.ContentItemSchemaService"/> remains responsible for recursive schema
/// composition and cycle protection.
/// </remarks>
public interface IContainedContentPartSchemaDefinition
{
    /// <summary>
    /// Gets the part payload property that stores the contained content items.
    /// </summary>
    string NestedItemsPropertyName { get; }

    /// <summary>
    /// Resolves the allowed contained content type names for the current part attachment.
    /// </summary>
    /// <param name="context">The context for the specific part attachment being rendered.</param>
    /// <param name="knownContentTypeDefinitions">The known content types available to the schema composer.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    ValueTask<IReadOnlyList<string>> GetContainedContentTypesAsync(
        ContentPartSchemaContext context,
        IReadOnlyList<ContentTypeDefinition> knownContentTypeDefinitions,
        CancellationToken cancellationToken = default);
}
