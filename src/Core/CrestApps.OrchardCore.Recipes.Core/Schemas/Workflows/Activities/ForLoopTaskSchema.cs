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
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("From", WorkflowActivitySchemaBuilders.SyntaxExpression("An expression evaluating to the start value. A plain number is also accepted. Defaults to \"0\".", "JavaScript"));
        yield return ("LiquidFrom", WorkflowActivitySchemaBuilders.LiquidExpression("Legacy: the Liquid start value, used when 'From' has no syntax and 'Syntax' is 'Liquid'."));
        yield return ("To", WorkflowActivitySchemaBuilders.SyntaxExpression("An expression evaluating to the exclusive end value. A plain number is also accepted. Defaults to \"10\".", "JavaScript"));
        yield return ("LiquidTo", WorkflowActivitySchemaBuilders.LiquidExpression("Legacy: the Liquid end value, used when 'To' has no syntax and 'Syntax' is 'Liquid'."));
        yield return ("Step", WorkflowActivitySchemaBuilders.SyntaxExpression("An expression evaluating to the increment applied after each iteration. A plain number is also accepted. Defaults to \"1\".", "JavaScript"));
        yield return ("LiquidStep", WorkflowActivitySchemaBuilders.LiquidExpression("Legacy: the Liquid increment, used when 'Step' has no syntax and 'Syntax' is 'Liquid'."));
        yield return ("Syntax", WorkflowActivitySchemaBuilders.EnumValue("Legacy: selects the JavaScript expression or its Liquid counterpart when the expression has no 'Syntax' of its own. Defaults to 'JavaScript'. Prefer setting 'Syntax' on the expression.", "JavaScript", "Liquid"));
        yield return ("LoopVariableName", WorkflowActivitySchemaBuilders.String("The name of the looping variable. The current value of each iteration is set to a workflow property with this name as well as to the last result. Defaults to \"x\"."));
        yield return ("Index", WorkflowActivitySchemaBuilders.Number("Runtime state. Holds the current index of the iteration and is normally omitted from recipes."));
    }
}
