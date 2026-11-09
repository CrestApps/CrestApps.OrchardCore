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
        yield return ("Enumerable", WorkflowActivitySchemaBuilders.SyntaxExpression("An expression evaluating to the enumerable object to iterate over. With Liquid, a JSON array, a comma-separated list or a single value are all accepted.", "JavaScript"));
        yield return ("LiquidEnumerable", WorkflowActivitySchemaBuilders.LiquidExpression("Legacy: the Liquid enumerable expression, used when 'Enumerable' has no syntax and 'Syntax' is 'Liquid'."));
        yield return ("Syntax", WorkflowActivitySchemaBuilders.EnumValue("Legacy: selects the JavaScript expression or its Liquid counterpart when the expression has no 'Syntax' of its own. Defaults to 'JavaScript'. Prefer setting 'Syntax' on the expression.", "JavaScript", "Liquid"));
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
                    .AllOf(
                        new JsonSchemaBuilder()
                            .Properties(("Syntax", new JsonSchemaBuilder().Const("Liquid")))
                            .Required("Syntax"),
                        new JsonSchemaBuilder()
                            .Not(new JsonSchemaBuilder()
                                .Properties(("Enumerable", new JsonSchemaBuilder()
                                    .Properties(("Syntax", new JsonSchemaBuilder()
                                        .Type(SchemaValueType.String)
                                        .MinLength(1)))
                                    .Required("Syntax")))
                                .Required("Enumerable"))))
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
