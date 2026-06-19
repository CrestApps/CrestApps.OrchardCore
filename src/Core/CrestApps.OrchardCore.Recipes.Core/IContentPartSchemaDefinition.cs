using CrestApps.OrchardCore.Recipes.Core.Schemas;
using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core;

/// <summary>
/// Produces a JSON schema fragment describing the content item payload for a specific content part.
/// </summary>
public interface IContentPartSchemaDefinition
{
    /// <summary>
    /// Gets the Orchard Core content part name that this schema contributes payload schema for.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Builds the payload schema for the part when attached to a specific content type.
    /// </summary>
    /// <param name="context">The context describing the concrete part attachment being rendered.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    ValueTask<JsonSchemaBuilder> GetPartSchemaAsync(ContentPartSchemaContext context, CancellationToken cancellationToken = default);
}
