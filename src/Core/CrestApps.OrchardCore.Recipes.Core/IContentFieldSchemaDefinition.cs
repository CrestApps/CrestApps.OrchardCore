using CrestApps.OrchardCore.Recipes.Core.Schemas;
using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core;

/// <summary>
/// Produces a JSON schema fragment describing the content item payload for a specific content field.
/// </summary>
internal interface IContentFieldSchemaDefinition
{
    /// <summary>
    /// Gets the Orchard Core content field type name that this schema contributes payload schema for.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Builds the payload schema for the field when attached to a specific content part on a content type.
    /// </summary>
    /// <param name="context">The context describing the concrete field attachment being rendered.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    ValueTask<JsonSchemaBuilder> GetFieldSchemaAsync(ContentFieldSchemaContext context, CancellationToken cancellationToken = default);
}
