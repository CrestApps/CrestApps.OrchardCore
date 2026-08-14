using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>WhileLoopTask</c> workflow task.
/// </summary>
public sealed class WhileLoopTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "WhileLoopTask";

    /// <inheritdoc />
    protected override string Category => "Control Flow";

    /// <inheritdoc />
    protected override string DisplayText => "While Loop Task";

    /// <inheritdoc />
    protected override string Description => "Takes the 'Iterate' outcome while a boolean condition evaluates to true and the 'Done' outcome once it becomes false";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Iterate", "Done"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("Condition", WorkflowActivitySchemaBuilders.ScriptExpression("An expression evaluating to true or false. Required when 'Syntax' is 'JavaScript'."));
        yield return ("LiquidCondition", WorkflowActivitySchemaBuilders.LiquidExpression("An expression evaluating to true or false. Required when 'Syntax' is 'Liquid'."));
        yield return ("Syntax", WorkflowActivitySchemaBuilders.EnumValue("The syntax used to evaluate the condition. Defaults to 'JavaScript'.", "JavaScript", "Liquid"));
    }
}
