using CrestApps.OrchardCore.Recipes.Core.Schemas;
using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core;

/// <summary>
/// Produces a JSON schema fragment for a specific content definition settings payload.
/// </summary>
/// <remarks>
/// Implement this interface when a feature needs to contribute schema for the
/// <c>Settings</c> object of a content part or content field inside the
/// <c>ContentDefinition</c> and <c>ReplaceContentDefinition</c> recipe steps.
/// Use <see cref="Schemas.PartSettingsSchemaBase"/> for part settings definitions so the
/// implementation only has to supply the part name and inner settings schema.
/// Implement the interface directly when you need a field definition or another schema type
/// that does not fit one of the shared base classes.
/// </remarks>
public interface IContentDefinitionSchemaDefinition
{
    /// <summary>
    /// Gets whether this definition contributes a part schema or a field schema.
    /// </summary>
    ContentDefinitionSchemaType Type { get; }

    /// <summary>
    /// Gets the Orchard Core content definition name that the schema applies to.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Builds the schema fragment describing the settings payload for this content definition.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    ValueTask<JsonSchemaBuilder> GetSettingsSchemaAsync(CancellationToken cancellationToken = default);
}
