using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;

/// <summary>
/// Provides reusable JSON schema fragments for describing workflow activity properties.
/// </summary>
/// <remarks>
/// Workflow activities persist their editable state inside the <c>Properties</c> object of an activity
/// record. Values typed as <c>WorkflowExpression&lt;T&gt;</c> are serialized as an object holding a single
/// <c>Expression</c> string, which is why <see cref="Expression(string)"/> and its variants return an object
/// schema rather than a plain string schema. Orchard Core omits null values when it serializes an activity,
/// so an expression that was never filled in is persisted as an empty object; the schema therefore treats
/// <c>Expression</c> as optional and nullable.
/// </remarks>
public static class WorkflowActivitySchemaBuilders
{
    /// <summary>
    /// The suffix appended to descriptions of properties that support Liquid syntax.
    /// </summary>
    public const string LiquidSupportText = "Supports Liquid syntax.";

    /// <summary>
    /// The suffix appended to descriptions of properties that support JavaScript syntax.
    /// </summary>
    public const string ScriptSupportText = "Supports JavaScript syntax, for example \"input('Name')\".";

    /// <summary>
    /// Creates a schema for a <c>WorkflowExpression&lt;T&gt;</c> property.
    /// </summary>
    /// <param name="description">The description shown for the property.</param>
    public static JsonSchemaBuilder Expression(string description)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(("Expression", new JsonSchemaBuilder()
                .Type(SchemaValueType.String | SchemaValueType.Null)
                .Description("The expression evaluated at runtime.")))
            .AdditionalProperties(false)
            .Description(description);

    /// <summary>
    /// Creates a schema for a <c>WorkflowExpression&lt;T&gt;</c> property that is evaluated as Liquid.
    /// </summary>
    /// <param name="description">The description shown for the property.</param>
    public static JsonSchemaBuilder LiquidExpression(string description)
        => Expression(Combine(description, LiquidSupportText));

    /// <summary>
    /// Creates a schema for a <c>WorkflowExpression&lt;T&gt;</c> property that is evaluated as JavaScript.
    /// </summary>
    /// <param name="description">The description shown for the property.</param>
    public static JsonSchemaBuilder ScriptExpression(string description)
        => Expression(Combine(description, ScriptSupportText));

    /// <summary>
    /// Creates a schema for a boolean property.
    /// </summary>
    /// <param name="description">The description shown for the property.</param>
    public static JsonSchemaBuilder Boolean(string description)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Boolean)
            .Description(description);

    /// <summary>
    /// Creates a schema for an integer property.
    /// </summary>
    /// <param name="description">The description shown for the property.</param>
    public static JsonSchemaBuilder Integer(string description)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Integer)
            .Description(description);

    /// <summary>
    /// Creates a schema for a floating point property.
    /// </summary>
    /// <param name="description">The description shown for the property.</param>
    public static JsonSchemaBuilder Number(string description)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Number)
            .Description(description);

    /// <summary>
    /// Creates a schema for a property that accepts a value of any JSON type.
    /// </summary>
    /// <param name="description">The description shown for the property.</param>
    public static JsonSchemaBuilder Any(string description)
        => new JsonSchemaBuilder()
            .Description(description);

    /// <summary>
    /// Creates a schema for a string property.
    /// </summary>
    /// <param name="description">The description shown for the property.</param>
    public static JsonSchemaBuilder String(string description)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.String)
            .Description(description);

    /// <summary>
    /// Creates a schema for a string property that surfaces the provided values as non-restrictive
    /// suggestions while still allowing any custom value.
    /// </summary>
    /// <param name="description">The description shown for the property.</param>
    /// <param name="examples">The well-known values to surface as suggestions.</param>
    public static JsonSchemaBuilder String(string description, IEnumerable<string> examples)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.String)
            .WithSuggestions(examples)
            .Description(description);

    /// <summary>
    /// Creates a schema for a string property restricted to a known set of values.
    /// </summary>
    /// <param name="description">The description shown for the property.</param>
    /// <param name="values">The allowed values.</param>
    public static JsonSchemaBuilder StringEnum(string description, params string[] values)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.String)
            .Enum(values)
            .Description(description);

    /// <summary>
    /// Creates a schema for a .NET enum property.
    /// </summary>
    /// <remarks>
    /// Orchard Core serializes activity properties with a string enum converter that also accepts the
    /// underlying ordinal value, so both representations are valid in a recipe.
    /// </remarks>
    /// <param name="description">The description shown for the property.</param>
    /// <param name="names">The enum member names, in declaration order.</param>
    public static JsonSchemaBuilder EnumValue(string description, params string[] names)
    {
        ArgumentNullException.ThrowIfNull(names);

        return new JsonSchemaBuilder()
            .AnyOf(
                new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Enum(names),
                new JsonSchemaBuilder()
                    .Type(SchemaValueType.Integer)
                    .Enum(Enumerable.Range(0, names.Length).Select(value => (JsonNode)value)))
            .Description(Combine(description, $"Accepted values: {string.Join(", ", names)}."));
    }

    /// <summary>
    /// Creates a schema for an array of strings.
    /// </summary>
    /// <param name="description">The description shown for the property.</param>
    public static JsonSchemaBuilder StringArray(string description)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Array)
            .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))
            .Description(description);

    /// <summary>
    /// Creates a schema for an array of strings that surfaces the provided values as non-restrictive
    /// suggestions on each item while still allowing any custom value.
    /// </summary>
    /// <param name="description">The description shown for the property.</param>
    /// <param name="itemExamples">The well-known values to surface as suggestions on each array item.</param>
    public static JsonSchemaBuilder StringArray(string description, IEnumerable<string> itemExamples)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Array)
            .Items(new JsonSchemaBuilder()
                .Type(SchemaValueType.String)
                .WithSuggestions(itemExamples))
            .Description(description);

    /// <summary>
    /// Creates a schema for an array of strings restricted to a known set of values.
    /// </summary>
    /// <param name="description">The description shown for the property.</param>
    /// <param name="values">The allowed values.</param>
    public static JsonSchemaBuilder StringEnumArray(string description, params string[] values)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Array)
            .Items(new JsonSchemaBuilder().Type(SchemaValueType.String).Enum(values))
            .Description(description);

    /// <summary>
    /// Creates a schema for the <c>ActivityMetadata</c> property that every workflow activity supports.
    /// </summary>
    public static JsonSchemaBuilder ActivityMetadata()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(("Title", new JsonSchemaBuilder()
                .Type(SchemaValueType.String | SchemaValueType.Null)
                .Description("A custom title displayed for this activity in the workflow editor.")))
            .AdditionalProperties(false)
            .Description("Editor metadata shared by every workflow activity.");

    private static string Combine(string description, string suffix)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return suffix;
        }

        return description.EndsWith('.')
            ? $"{description} {suffix}"
            : $"{description}. {suffix}";
    }
}
