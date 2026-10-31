using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.Workforce.Deployments;

/// <summary>
/// Describes the Workforce deployment step's settings to CrestApps Recipes.
/// </summary>
internal sealed class WorkforceDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(WorkforceDeploymentStep);

    protected override string DisplayText => "Workforce teams and workers";

    protected override string Description => "Exports the activity codes and the team hierarchy, and optionally the workers.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("IncludeWorkers", DeploymentSchemaBuilders.Boolean("When true, exports every worker too, with their teams by name. Sensitive details such as the date of birth are left out."));
    }
}
