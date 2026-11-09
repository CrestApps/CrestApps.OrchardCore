using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>SetOutputTask</c> workflow task.
/// </summary>
public sealed class SetOutputTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "SetOutputTask";

    /// <inheritdoc />
    protected override string Category => "Primitives";

    /// <inheritdoc />
    protected override string DisplayText => "Set Output Task";

    /// <inheritdoc />
    protected override string Description => "Stores a value in the workflow output so it can be read by the caller once the workflow completes";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done"];

    /// <inheritdoc />
    protected override IEnumerable<string> RequiredProperties => ["OutputName"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("OutputName", WorkflowActivitySchemaBuilders.String("The name of the workflow output entry to write to."));
        yield return ("Value", WorkflowActivitySchemaBuilders.SyntaxExpression("The value to store in the specified output entry.", "JavaScript"));
        yield return ("LiquidValue", WorkflowActivitySchemaBuilders.LiquidExpression("Legacy: the Liquid value, used when 'Value' has no syntax and 'Syntax' is 'Liquid'."));
        yield return ("Syntax", WorkflowActivitySchemaBuilders.EnumValue("Legacy: selects the JavaScript expression or its Liquid counterpart when the expression has no 'Syntax' of its own. Defaults to 'JavaScript'. Prefer setting 'Syntax' on the expression.", "JavaScript", "Liquid"));
    }
}
