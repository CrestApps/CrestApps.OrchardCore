namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Whether a schema definition targets a content part or a content field.
/// </summary>
public enum ContentDefinitionSchemaType
{
    Part,
    Field,
}

/// <summary>
/// Produces a JSON Schema fragment describing the settings of a particular
/// content part or field definition.
/// </summary>
public interface IContentDefinitionSchemaDefinition
{
    ContentDefinitionSchemaType Type { get; }

    string Name { get; }

    ValueTask<JsonSchema> GetSettingsSchemaAsync();
}

/// <summary>
/// Handy base that targets <see cref="ContentDefinitionSchemaType.Part"/>,
/// caches the schema after first construction, and exposes a helper to
/// wrap inner settings inside the standard envelope.
/// </summary>
public abstract class PartSettingsSchemaBase : IContentDefinitionSchemaDefinition
{
    private JsonSchema _cache;

    public ContentDefinitionSchemaType Type => ContentDefinitionSchemaType.Part;

    public abstract string Name { get; }

    public ValueTask<JsonSchema> GetSettingsSchemaAsync()
    {
        _cache ??= BuildSettingsCore();
        return ValueTask.FromResult(_cache);
    }

    protected abstract JsonSchema BuildSettingsCore();

    /// <summary>
    /// Wraps <paramref name="innerSettings"/> under a top-level object
    /// property keyed by <paramref name="settingsKey"/>.
    /// </summary>
    protected static JsonSchema Envelope(string settingsKey, JsonSchemaBuilder innerSettings)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties((settingsKey, innerSettings))
            .AdditionalProperties(true)
            .Build();
}
