using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>SetVariableTask</c> workflow task.
/// </summary>
public sealed class SetVariableTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "SetVariableTask";

    /// <inheritdoc />
    protected override string Category => "Primitives";

    /// <inheritdoc />
    protected override string DisplayText => "Set Variable Task";

    /// <inheritdoc />
    protected override string Description => "Sets a workflow variable to the result of an expression. A declared variable gets the value converted to its type, and a value that does not convert faults the workflow";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done"];

    /// <inheritdoc />
    protected override IEnumerable<string> RequiredProperties => ["VariableName"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("VariableName", WorkflowActivitySchemaBuilders.String("The name of the variable to set, usually one declared in the 'Variables' of the workflow type."));
        yield return ("Value", WorkflowActivitySchemaBuilders.SyntaxExpression("The value of the variable.", "JavaScript"));
        yield return ("LiquidValue", WorkflowActivitySchemaBuilders.LiquidExpression("Legacy: the Liquid value, used when 'Value' has no syntax and 'Syntax' is 'Liquid'."));
        yield return ("Syntax", WorkflowActivitySchemaBuilders.EnumValue("Legacy: selects the JavaScript expression or its Liquid counterpart when the expression has no 'Syntax' of its own. Defaults to 'JavaScript'. Prefer setting 'Syntax' on the expression.", "JavaScript", "Liquid"));
    }
}
