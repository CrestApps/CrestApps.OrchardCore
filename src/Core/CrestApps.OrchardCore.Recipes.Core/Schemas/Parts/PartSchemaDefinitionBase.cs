using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Parts;

/// <summary>
/// Provides the standard implementation surface for content part settings schema definitions.
/// </summary>
/// <remarks>
/// Use this base class when a feature contributes JSON schema for a content part's
/// <c>Settings</c> object inside the <c>ContentDefinition</c> or
/// <c>ReplaceContentDefinition</c> recipe steps. Implementations only need to supply the
/// part name and the part-specific schema fragments; this base class handles the
/// <see cref="ContentDefinitionSchemaType.Part"/> classification and schema caching.
/// </remarks>
public abstract class PartSchemaDefinitionBase : IContentSchemaDefinition, IContentPartSchemaDefinition
{
    private JsonSchemaBuilder _cachedPartSchema;
    private JsonSchemaBuilder _cachedSchema;

    /// <summary>
    /// Gets the schema definition category used when composing content definition steps.
    /// </summary>
    public ContentDefinitionSchemaType Type { get; } = ContentDefinitionSchemaType.Part;

    /// <summary>
    /// Gets the Orchard Core content part name that this schema contributes settings for.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Gets the JSON schema fragment for the part settings envelope.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public ValueTask<JsonSchemaBuilder> GetSettingsSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cachedSchema ??= BuildSettingsCore();

        return ValueTask.FromResult(_cachedSchema);
    }

    ValueTask<JsonSchemaBuilder> IContentPartSchemaDefinition.GetPartSchemaAsync(CancellationToken cancellationToken)
    {
        _cachedPartSchema ??= BuildPartSchemaCore();

        return ValueTask.FromResult(_cachedPartSchema);
    }

    /// <summary>
    /// Builds the part-specific settings schema.
    /// </summary>
    /// <remarks>
    /// Return the full fragment that will be merged into the part <c>Settings</c> object.
    /// </remarks>
    protected abstract JsonSchemaBuilder BuildSettingsCore();

    /// <summary>
    /// Builds the content item payload schema for the content part.
    /// </summary>
    protected virtual JsonSchemaBuilder BuildPartSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .AdditionalProperties(true);
}
