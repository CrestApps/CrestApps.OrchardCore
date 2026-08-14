using CrestApps.OrchardCore.Recipes.Core.Schemas;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Fields;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Parts;
using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core;

/// <summary>
/// Produces a JSON schema fragment for a specific content definition settings payload.
/// </summary>
/// <remarks>
/// Implement this interface when a feature needs to contribute schema for the
/// <c>Settings</c> object of a content part or content field inside the
/// <c>ContentDefinition</c> and <c>ReplaceContentDefinition</c> recipe steps.
/// Multiple implementations can share the same <see cref="Name"/>; their schema fragments
/// are combined into the final schema for that Orchard Core part or field type.
/// Use <see cref="PartSchemaDefinitionBase"/> for part definitions and
/// <see cref="FieldSchemaDefinitionBase"/> for field definitions so implementations only
/// need to supply the Orchard name plus the settings and payload schema fragments that are
/// specific to that part or field.
/// Implement the interface directly only when the contribution does not fit either shared
/// base class.
/// </remarks>
public interface IContentSchemaDefinition
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
