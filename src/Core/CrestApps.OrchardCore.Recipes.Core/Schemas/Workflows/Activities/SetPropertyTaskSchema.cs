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
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions()
    {
        yield return ("PropertyName", WorkflowActivitySchemaBuilders.String("The workflow property to set. The property is added when it does not exist yet."));
        yield return ("Value", WorkflowActivitySchemaBuilders.ScriptExpression("The value to store in the specified property. Required when 'Syntax' is 'JavaScript'."));
        yield return ("LiquidValue", WorkflowActivitySchemaBuilders.LiquidExpression("The value to store in the specified property. Required when 'Syntax' is 'Liquid'."));
        yield return ("Syntax", WorkflowActivitySchemaBuilders.EnumValue("The syntax used to evaluate the value. Defaults to 'JavaScript'.", "JavaScript", "Liquid"));
    }
}
