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
    /// The names of the built-in workflow expression syntaxes.
    /// </summary>
    public static readonly string[] ExpressionSyntaxes = ["Literal", "Liquid", "JavaScript"];

    /// <summary>
    /// The names of the built-in types of workflow variables.
    /// </summary>
    public static readonly string[] VariableTypeNames = ["any", "array", "boolean", "contentItem", "datetime", "number", "object", "string"];

    /// <summary>
    /// Creates a schema for a <c>WorkflowExpression&lt;T&gt;</c> property.
    /// </summary>
    /// <param name="description">The description shown for the property.</param>
    public static JsonSchemaBuilder Expression(string description)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Expression", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String | SchemaValueType.Null)
                    .Description("The expression evaluated at runtime.")),
                ("Syntax", ExpressionSyntax()
                    .Description("The syntax of the expression: 'Literal', 'Liquid', 'JavaScript' or another registered syntax. Only the activities that let each expression choose its syntax read it; the other activities always use their own syntax.")))
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
    /// Creates a schema for a <c>WorkflowExpression&lt;T&gt;</c> property of an activity that lets the expression
    /// choose its syntax.
    /// </summary>
    /// <param name="description">The description of the property.</param>
    /// <param name="defaultSyntax">The syntax used when neither the expression nor the activity selects one.</param>
    public static JsonSchemaBuilder SyntaxExpression(string description, string defaultSyntax)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Expression", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String | SchemaValueType.Null)
                    .Description("The expression evaluated at runtime.")),
                ("Syntax", ExpressionSyntax()
                    .Description($"How the expression is evaluated: 'Literal' (the text is the value), 'Liquid', 'JavaScript', or another registered syntax. When omitted, the legacy 'Syntax' property of the activity decides, and {defaultSyntax} is used by default.")))
            .AdditionalProperties(false)
            .Description(description);

    /// <summary>
    /// Creates the schema of the name of a workflow expression syntax.
    /// </summary>
    public static JsonSchemaBuilder ExpressionSyntax()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.String | SchemaValueType.Null)
            .WithSuggestions(ExpressionSyntaxes);

    /// <summary>
    /// Creates the schema of the output bindings of an activity, which write its outputs to workflow variables.
    /// </summary>
    public static JsonSchemaBuilder OutputBindings()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .AdditionalProperties(new JsonSchemaBuilder().Type(SchemaValueType.String))
            .Description("Writes outputs of the activity to workflow variables: an object of output names, such as 'Result', to the names of declared variables.");

    /// <summary>
    /// Creates the schema of the retry policy of a task.
    /// </summary>
    public static JsonSchemaBuilder ActivityRetryPolicy()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("MaxRetries", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Integer)
                    .Minimum(0)
                    .Maximum(10)
                    .Description("How many times the task is retried when it faults, from 0 to 10.")),
                ("DelaySeconds", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Integer)
                    .Minimum(0)
                    .Description("The delay before the first retry, in seconds. With 0, the task is retried at once, in the same run.")),
                ("Backoff", EnumValue("How the delay grows from one retry to the next. Defaults to 'Fixed'.", "Fixed", "Exponential")),
                ("OnFailure", EnumValue("What the failure does once the attempts are spent: fault the workflow, or follow the 'Failed' outcome. Defaults to 'FaultWorkflow'.", "FaultWorkflow", "FollowFailedOutcome")))
            .AdditionalProperties(true)
            .Description("How the task is retried when it faults, and what its failure does once the attempts are spent. When 'OnFailure' is 'FollowFailedOutcome', the task gets a 'Failed' outcome.");

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
