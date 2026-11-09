using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>SetPropertyTask</c> workflow task.
/// </summary>
public sealed class SetPropertyTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "SetPropertyTask";

    /// <inheritdoc />
    protected override string Category => "Primitives";

    /// <inheritdoc />
    protected override string DisplayText => "Set Property Task";

    /// <inheritdoc />
    protected override string Description => "Stores a value in a workflow property so later activities can read it";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done"];

    /// <inheritdoc />
    protected override IEnumerable<string> RequiredProperties => ["PropertyName"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("PropertyName", WorkflowActivitySchemaBuilders.String("The workflow property to set. The property is added when it does not exist yet."));
        yield return ("Value", WorkflowActivitySchemaBuilders.SyntaxExpression("The value to store in the specified property.", "JavaScript"));
        yield return ("LiquidValue", WorkflowActivitySchemaBuilders.LiquidExpression("Legacy: the Liquid value, used when 'Value' has no syntax and 'Syntax' is 'Liquid'."));
        yield return ("Syntax", WorkflowActivitySchemaBuilders.EnumValue("Legacy: selects the JavaScript expression or its Liquid counterpart when the expression has no 'Syntax' of its own. Defaults to 'JavaScript'. Prefer setting 'Syntax' on the expression.", "JavaScript", "Liquid"));
    }
}
