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
        yield return ("Value", WorkflowActivitySchemaBuilders.ScriptExpression("The value to store in the specified output entry. Required when 'Syntax' is 'JavaScript'."));
        yield return ("LiquidValue", WorkflowActivitySchemaBuilders.LiquidExpression("The value to store in the specified output entry. Required when 'Syntax' is 'Liquid'."));
        yield return ("Syntax", WorkflowActivitySchemaBuilders.EnumValue("The syntax used to evaluate the value. Defaults to 'JavaScript'.", "JavaScript", "Liquid"));
    }
}
