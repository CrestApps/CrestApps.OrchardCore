using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using CrestApps.OrchardCore.Workforce.Services;
using Json.Schema;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Recipes;

/// <summary>
/// Describes the Agent Out Of Adherence workflow event to CrestApps Recipes.
/// </summary>
internal sealed class WorkforceOutOfAdherenceEventSchema : WorkflowActivitySchemaDefinitionBase
{
    public override string Name => WorkforceEvents.OutOfAdherence;

    protected override string Category => "Workforce";

    protected override string DisplayText => "Agent Out Of Adherence";

    protected override string Description => "Starts when an agent has been out of adherence with their schedule for longer than their adherence rule allows. Input: WorkerId, WorkerName, TeamId, Status, Reason, ScheduledActivity, OutSinceUtc, Minutes";

    protected override IEnumerable<string> Outcomes => ["Done"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("TeamIds", WorkflowActivitySchemaBuilders.StringArray("The ids of the teams the event starts for, matched against the TeamId of its input; empty for every team."));
    }
}
