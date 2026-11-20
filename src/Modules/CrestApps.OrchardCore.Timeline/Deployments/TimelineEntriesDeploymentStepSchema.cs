using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.Timeline.Deployments;

/// <summary>
/// Describes the timeline entries deployment step to CrestApps Recipes.
/// </summary>
internal sealed class TimelineEntriesDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(TimelineEntriesDeploymentStep);

    protected override string DisplayText => "Timeline entries";

    protected override string Description => "Exports the timeline entries of some kinds of records, oldest first.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return (nameof(TimelineEntriesDeploymentStep.SubjectKinds), DeploymentSchemaBuilders.String("The record kinds whose entries are exported, comma-separated, such as WorkOrder,Order. Nothing is exported when it is empty."));
    }
}
