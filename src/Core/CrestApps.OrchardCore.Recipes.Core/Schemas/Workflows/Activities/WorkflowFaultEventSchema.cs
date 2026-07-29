using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>WorkflowFaultEvent</c> workflow event.
/// </summary>
public sealed class WorkflowFaultEventSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "WorkflowFaultEvent";

    /// <inheritdoc />
    protected override string Category => "Background";

    /// <inheritdoc />
    protected override string DisplayText => "Catch Workflow Fault Event";

    /// <inheritdoc />
    protected override string Description => "Starts the workflow when another workflow faults and the trigger condition matches the captured error information";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions()
    {
        yield return ("ErrorFilter", WorkflowActivitySchemaBuilders.ScriptExpression("Inspects the captured workflow error information and returns a boolean. The workflow starts only when the expression returns true. Read the error details with input('WorkflowFault'), which exposes WorkflowName, WorkflowId, ErrorMessage, ExceptionDetails, FaultMessage, ActivityDisplayName, ActivityTypeName, ActivityId and ExecutedActivityCount."));
    }
}
