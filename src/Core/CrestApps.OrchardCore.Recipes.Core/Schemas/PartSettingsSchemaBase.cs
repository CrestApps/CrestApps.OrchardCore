using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Provides the standard implementation surface for content part settings schema definitions.
/// </summary>
/// <remarks>
/// Use this base class when a feature contributes JSON schema for a content part's
/// <c>Settings</c> object inside the <c>ContentDefinition</c> or
/// <c>ReplaceContentDefinition</c> recipe steps. Implementations only need to supply the
/// part name and the inner part-specific settings object; this base class handles the
/// <see cref="ContentDefinitionSchemaType.Part"/> classification, schema caching, and the
/// common JSON-schema helpers used by the built-in part definitions.
/// </remarks>
public abstract class PartSettingsSchemaBase : IContentDefinitionSchemaDefinition
{
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

    /// <summary>
    /// Builds the part-specific settings schema.
    /// </summary>
    /// <remarks>
    /// Return the full fragment that will be merged into the part <c>Settings</c> object.
    /// Most implementations should call <see cref="Envelope(string, JsonSchemaBuilder)"/>
    /// with the Orchard settings type name, such as <c>TitlePartSettings</c>, and an inner
    /// object built with the helper methods on this base class.
    /// </remarks>
    protected abstract JsonSchemaBuilder BuildSettingsCore();

    /// <summary>
    /// Wraps <paramref name="innerSettings"/> under a top-level object property keyed by <paramref name="settingsKey"/>.
    /// </summary>
    protected static JsonSchemaBuilder Envelope(string settingsKey, JsonSchemaBuilder innerSettings)
    {
       return new JsonSchemaBuilder()
           .Type(SchemaValueType.Object)
           .Properties((settingsKey, innerSettings))
           .AdditionalProperties(true);
    }

    protected static JsonSchemaBuilder BoolProp()
    {
       return new JsonSchemaBuilder().Type(SchemaValueType.Boolean);
    }

    protected static JsonSchemaBuilder StringArray()
    {
       return new JsonSchemaBuilder()
           .Type(SchemaValueType.Array)
           .Items(new JsonSchemaBuilder().Type(SchemaValueType.String));
    }

    protected static (string, JsonSchemaBuilder) Prop(string name, JsonSchemaBuilder schema)
    {
       return (name, schema);
    }

    /// <summary>
    /// Builds a settings-object with <c>additionalProperties: false</c>.
    /// </summary>
    protected static JsonSchemaBuilder Obj(params (string, JsonSchemaBuilder)[] props)
    {
       return new JsonSchemaBuilder()
           .Type(SchemaValueType.Object)
           .Properties(props)
           .AdditionalProperties(false);
    }
}
