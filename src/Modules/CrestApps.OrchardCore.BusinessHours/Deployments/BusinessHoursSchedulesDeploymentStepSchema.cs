using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.BusinessHours.Deployments;

/// <summary>
/// Describes the business hours schedules deployment step to CrestApps Recipes.
/// </summary>
internal sealed class BusinessHoursSchedulesDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(BusinessHoursSchedulesDeploymentStep);

    protected override string DisplayText => "Business hours schedules";

    protected override string Description => "Exports every business hours schedule.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
        => [];
}
