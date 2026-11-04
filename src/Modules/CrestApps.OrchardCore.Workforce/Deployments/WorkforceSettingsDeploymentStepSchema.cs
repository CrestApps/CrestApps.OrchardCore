using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.Workforce.Deployments;

/// <summary>
/// Describes the Workforce settings deployment step to CrestApps Recipes.
/// </summary>
internal sealed class WorkforceSettingsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(WorkforceSettingsDeploymentStep);

    protected override string DisplayText => "Workforce settings";

    protected override string Description => "Exports shift templates, time-off settings and time clock settings, for the features that are on.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield break;
    }
}
