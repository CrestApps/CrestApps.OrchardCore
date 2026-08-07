using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>ForEachTask</c> workflow task.
/// </summary>
public sealed class ForEachTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "ForEachTask";

    /// <inheritdoc />
    protected override string Category => "Control Flow";

    /// <inheritdoc />
    protected override string DisplayText => "For Each Task";

    /// <inheritdoc />
    protected override string Description => "Iterates over an enumerable value, taking the 'Iterate' outcome once per item and the 'Done' outcome when the enumeration completes";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Iterate", "Done"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("Enumerable", WorkflowActivitySchemaBuilders.ScriptExpression("An expression evaluating to the enumerable object to iterate over. Required when 'Syntax' is 'JavaScript'."));
        yield return ("LiquidEnumerable", WorkflowActivitySchemaBuilders.LiquidExpression("An expression evaluating to the enumerable object to iterate over. A JSON array, a comma-separated list or a single value are all accepted. Required when 'Syntax' is 'Liquid'."));
        yield return ("Syntax", WorkflowActivitySchemaBuilders.EnumValue("The syntax used to evaluate the enumerable expression. Defaults to 'JavaScript'.", "JavaScript", "Liquid"));
        yield return ("LoopVariableName", WorkflowActivitySchemaBuilders.String("The name of the looping variable. The current value of each iteration is set to a workflow property with this name as well as to the last result. Defaults to \"x\"."));
        yield return ("Current", WorkflowActivitySchemaBuilders.Any("Runtime state. Holds the value of the current iteration and is normally omitted from recipes."));
        yield return ("Index", WorkflowActivitySchemaBuilders.Integer("Runtime state. Holds the number of iterations executed so far and is normally omitted from recipes."));
    }

    /// <inheritdoc />
    protected override WorkflowActivitySchema BuildActivitySchemaCore(WorkflowActivitySchemaContext context)
    {
        var schema = base.BuildActivitySchemaCore(context);

        schema.Properties = schema.Properties
            .AllOf(new JsonSchemaBuilder()
                .If(new JsonSchemaBuilder()
                    .Properties(("Syntax", new JsonSchemaBuilder().Const("Liquid")))
                    .Required("Syntax"))
                .Then(RequireExpression("LiquidEnumerable"))
                .Else(RequireExpression("Enumerable")));

        return schema;
    }

    private static JsonSchemaBuilder RequireExpression(string propertyName)
        => new JsonSchemaBuilder()
            .Properties((propertyName, new JsonSchemaBuilder()
                .Properties(("Expression", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .MinLength(1)))
                .Required("Expression")))
            .Required(propertyName);
}
