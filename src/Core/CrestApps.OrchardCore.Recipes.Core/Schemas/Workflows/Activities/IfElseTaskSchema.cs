using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>IfElseTask</c> workflow task.
/// </summary>
public sealed class IfElseTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "IfElseTask";

    /// <inheritdoc />
    protected override string Category => "Control Flow";

    /// <inheritdoc />
    protected override string DisplayText => "If Else Task";

    /// <inheritdoc />
    protected override string Description => "Evaluates a boolean condition and takes the 'True' or 'False' outcome accordingly";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["True", "False"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("Condition", WorkflowActivitySchemaBuilders.SyntaxExpression("An expression evaluating to true or false.", "JavaScript"));
        yield return ("LiquidCondition", WorkflowActivitySchemaBuilders.LiquidExpression("Legacy: the Liquid condition, used when 'Condition' has no syntax and 'Syntax' is 'Liquid'."));
        yield return ("Syntax", WorkflowActivitySchemaBuilders.EnumValue("Legacy: selects the JavaScript expression or its Liquid counterpart when the expression has no 'Syntax' of its own. Defaults to 'JavaScript'. Prefer setting 'Syntax' on the expression.", "JavaScript", "Liquid"));
    }
}
