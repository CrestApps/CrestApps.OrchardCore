using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>CorrelateTask</c> workflow task.
/// </summary>
public sealed class CorrelateTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "CorrelateTask";

    /// <inheritdoc />
    protected override string Category => "Primitives";

    /// <inheritdoc />
    protected override string DisplayText => "Correlate Task";

    /// <inheritdoc />
    protected override string Description => "Sets the correlation identifier of the workflow instance so it can be resumed by a matching event";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done"];

    /// <inheritdoc />
    protected override IEnumerable<string> RequiredProperties => ["Value"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions()
    {
        yield return ("Value", WorkflowActivitySchemaBuilders.Expression("The value to assign to the workflow correlation identifier. Evaluated using the syntax selected in 'Syntax'."));
        yield return ("Syntax", WorkflowActivitySchemaBuilders.EnumValue("The syntax used to evaluate 'Value'. Defaults to 'JavaScript'.", "JavaScript", "Liquid"));
    }
}
