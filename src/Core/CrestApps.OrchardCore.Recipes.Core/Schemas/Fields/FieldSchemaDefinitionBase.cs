using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Fields;

/// <summary>
/// Provides the standard implementation surface for content field schema definitions.
/// </summary>
public abstract class FieldSchemaDefinitionBase : IContentSchemaDefinition, IContentFieldSchemaDefinition
{
    private JsonSchemaBuilder _cachedFieldSchema;
    private JsonSchemaBuilder _cachedSettingsSchema;

    /// <summary>
    /// Gets the schema definition category used when composing content definition steps.
    /// </summary>
    public ContentDefinitionSchemaType Type { get; } = ContentDefinitionSchemaType.Field;

    /// <summary>
    /// Gets the Orchard Core content field name that this schema contributes settings for.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Gets the JSON schema fragment for the field settings payload.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public ValueTask<JsonSchemaBuilder> GetSettingsSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cachedSettingsSchema ??= BuildSettingsCore();

        return ValueTask.FromResult(_cachedSettingsSchema);
    }

    ValueTask<JsonSchemaBuilder> IContentFieldSchemaDefinition.GetFieldSchemaAsync(CancellationToken cancellationToken)
    {
        _cachedFieldSchema ??= BuildFieldSchemaCore();

        return ValueTask.FromResult(_cachedFieldSchema);
    }

    /// <summary>
    /// Builds the field-specific settings schema.
    /// </summary>
    protected abstract JsonSchemaBuilder BuildSettingsCore();

    /// <summary>
    /// Builds the field value schema used by content item recipe payloads.
    /// </summary>
    protected abstract JsonSchemaBuilder BuildFieldSchemaCore();
}
