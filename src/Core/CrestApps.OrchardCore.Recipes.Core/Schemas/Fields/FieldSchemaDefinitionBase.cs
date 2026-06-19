using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Fields;

/// <summary>
/// Provides the standard implementation surface for content field schema definitions.
/// </summary>
public abstract class FieldSchemaDefinitionBase : IContentSchemaDefinition, IContentFieldSchemaDefinition
{
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

    ValueTask<JsonSchemaBuilder> IContentFieldSchemaDefinition.GetFieldSchemaAsync(
        ContentFieldSchemaContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return BuildFieldSchemaAsync(context, cancellationToken);
    }

    /// <summary>
    /// Builds the field-specific settings schema.
    /// </summary>
    protected abstract JsonSchemaBuilder BuildSettingsCore();

    /// <summary>
    /// Builds the field value schema used by content item recipe payloads.
    /// Override this method when the schema requires asynchronous work.
    /// </summary>
    /// <param name="context">The context describing the concrete field attachment.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    protected virtual ValueTask<JsonSchemaBuilder> BuildFieldSchemaAsync(
        ContentFieldSchemaContext context,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(BuildFieldSchemaCore());

    /// <summary>
    /// Builds the field value schema used by content item recipe payloads.
    /// </summary>
    protected abstract JsonSchemaBuilder BuildFieldSchemaCore();
}
