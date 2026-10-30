using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>AICallActionTask</c> workflow task.
/// </summary>
public sealed class AICallActionTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "AICallActionTask";

    /// <inheritdoc />
    protected override string Category => "AI Conversations";

    /// <inheritdoc />
    protected override string DisplayText => "AI Call Action";

    /// <inheritdoc />
    protected override string Description => "Messages the AI on a live call, hands the call to an agent or a phone number, switches its AI profile, or ends it";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    /// <inheritdoc />
    protected override IEnumerable<string> RequiredProperties => ["Action"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("Action", WorkflowActivitySchemaBuilders.StringEnum("What to do.", "Message", "TakeOver", "TransferToNumber", "SwitchProfile", "End"));
        yield return ("Value", WorkflowActivitySchemaBuilders.LiquidExpression("The note, the agent's user name, the phone number, or the AI profile identifier, depending on the action."));
        yield return ("ActivityId", WorkflowActivitySchemaBuilders.LiquidExpression("The call's activity. Defaults to {{ Workflow.Input.ActivityId }}."));
    }
}
