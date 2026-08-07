using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Rules;

/// <summary>
/// Provides reusable JSON schema fragments for describing rule condition and operator members.
/// </summary>
public static class RuleConditionSchemaBuilders
{
    /// <summary>
    /// Creates a schema for a string property.
    /// </summary>
    /// <param name="description">The description shown for the property.</param>
    public static JsonSchemaBuilder String(string description)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.String)
            .Description(description);

    /// <summary>
    /// Creates a schema for a nullable string property.
    /// </summary>
    /// <param name="description">The description shown for the property.</param>
    public static JsonSchemaBuilder NullableString(string description)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.String | SchemaValueType.Null)
            .Description(description);

    /// <summary>
    /// Creates a schema for a boolean property.
    /// </summary>
    /// <param name="description">The description shown for the property.</param>
    public static JsonSchemaBuilder Boolean(string description)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Boolean)
            .Description(description);
}
