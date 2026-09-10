using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>SetAgentPresenceTask</c> workflow task.
/// </summary>
public sealed class SetAgentPresenceTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "SetAgentPresenceTask";

    /// <inheritdoc />
    protected override string Category => "Contact Center";

    /// <inheritdoc />
    protected override string DisplayText => "Set Agent Presence";

    /// <inheritdoc />
    protected override string Description => "Sets a Contact Center agent's presence status.";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("UserId", WorkflowActivitySchemaBuilders.String("The Liquid expression that resolves the Orchard user identifier of the agent."));
        yield return ("Status", WorkflowActivitySchemaBuilders.EnumValue("The presence status to apply to the agent.", "Offline", "Available", "Reserved", "Busy", "WrapUp", "Break", "RequestBreak", "Away", "DoNotDisturb", "Meeting", "Training", "AfterHoursUnavailable"));
        yield return ("Reason", WorkflowActivitySchemaBuilders.String("The optional Liquid expression that resolves the reason recorded with the change."));
    }
}
