using CrestApps.OrchardCore.Omnichannel.Managements.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Workflows.Models;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Json.Schema;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Schemas;

/// <summary>
/// Describes the recipe schema of the <c>RerouteActivityTask</c> workflow task.
/// </summary>
internal sealed class RerouteActivityTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = nameof(RerouteActivityTask);

    /// <inheritdoc />
    protected override string Category => "Contact Center";

    /// <inheritdoc />
    protected override string DisplayText => "Reroute Activity";

    /// <inheritdoc />
    protected override string Description => "Moves an activity to an agent or to the dialer, or to another campaign or dialer profile. A finished activity's open follow-ups are moved instead, and its next attempt can be created already moved when it has none. Writes the ids it moved to the workflow output ReroutedActivityItemIds";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Rerouted", "Nothing To Reroute", "Failed"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return (nameof(RerouteActivityTask.ActivityItemId), WorkflowActivitySchemaBuilders.String($"The Liquid expression that resolves the activity. Defaults to '{RerouteActivityTask.DefaultActivityExpression}'."));
        yield return (nameof(RerouteActivityTask.Target), WorkflowActivitySchemaBuilders.EnumValue(
            "Who makes the next call: Agent (a manual call; the activity leaves the dialer), Dialer (under DialerProfileId, on the campaign's dialer queue), or KeepRoute (only the campaign changes). Defaults to Agent.",
            nameof(ActivityRerouteTarget.Agent),
            nameof(ActivityRerouteTarget.Dialer),
            nameof(ActivityRerouteTarget.KeepRoute)));
        yield return (nameof(RerouteActivityTask.CampaignId), WorkflowActivitySchemaBuilders.String("The campaign the activity moves to. Empty keeps its campaign; required for KeepRoute."));
        yield return (nameof(RerouteActivityTask.DialerProfileId), WorkflowActivitySchemaBuilders.String("The dialer profile that dials the activity. Required for Dialer."));
        yield return (nameof(RerouteActivityTask.Source), WorkflowActivitySchemaBuilders.String("For Agent: the activity source, one that may be set by hand. Empty is Manual."));
        yield return (nameof(RerouteActivityTask.UserName), WorkflowActivitySchemaBuilders.String("For Agent: the Liquid expression that resolves the user name of the agent who makes the call, such as '{{ Workflow.Input.AgentUserName }}'. Empty keeps the owner the activity has."));
        yield return (nameof(RerouteActivityTask.CreateNextAttempt), WorkflowActivitySchemaBuilders.Boolean("Whether a finished activity with no open follow-up gets its next attempt created, already rerouted. Defaults to true."));
        yield return (nameof(RerouteActivityTask.DelayMinutes), WorkflowActivitySchemaBuilders.String("The Liquid expression that resolves how many minutes from now a created next attempt is due. Empty is due now."));
    }
}
