using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>ForLoopTask</c> workflow task.
/// </summary>
public sealed class ForLoopTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "ForLoopTask";

    /// <inheritdoc />
    protected override string Category => "Control Flow";

    /// <inheritdoc />
    protected override string DisplayText => "For Loop Task";

    /// <inheritdoc />
    protected override string Description => "Iterates from a start value to an end value, taking the 'Iterate' outcome once per step and the 'Done' outcome when the loop completes";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Iterate", "Done"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions()
    {
        yield return ("From", WorkflowActivitySchemaBuilders.ScriptExpression("An expression evaluating to the start value. A plain number is also accepted. Required when 'Syntax' is 'JavaScript'. Defaults to \"0\"."));
        yield return ("LiquidFrom", WorkflowActivitySchemaBuilders.LiquidExpression("An expression evaluating to the start value. Required when 'Syntax' is 'Liquid'. Defaults to \"0\"."));
        yield return ("To", WorkflowActivitySchemaBuilders.ScriptExpression("An expression evaluating to the exclusive end value. A plain number is also accepted. Required when 'Syntax' is 'JavaScript'. Defaults to \"10\"."));
        yield return ("LiquidTo", WorkflowActivitySchemaBuilders.LiquidExpression("An expression evaluating to the exclusive end value. Required when 'Syntax' is 'Liquid'. Defaults to \"10\"."));
        yield return ("Step", WorkflowActivitySchemaBuilders.ScriptExpression("An expression evaluating to the increment applied after each iteration. A plain number is also accepted. Required when 'Syntax' is 'JavaScript'. Defaults to \"1\"."));
        yield return ("LiquidStep", WorkflowActivitySchemaBuilders.LiquidExpression("An expression evaluating to the increment applied after each iteration. Required when 'Syntax' is 'Liquid'. Defaults to \"1\"."));
        yield return ("Syntax", WorkflowActivitySchemaBuilders.EnumValue("The syntax used to evaluate the loop expressions. Defaults to 'JavaScript'.", "JavaScript", "Liquid"));
        yield return ("LoopVariableName", WorkflowActivitySchemaBuilders.String("The name of the looping variable. The current value of each iteration is set to a workflow property with this name as well as to the last result. Defaults to \"x\"."));
        yield return ("Index", WorkflowActivitySchemaBuilders.Number("Runtime state. Holds the current index of the iteration and is normally omitted from recipes."));
    }
}
