using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.Scheduling.Deployments;

/// <summary>
/// Describes the Scheduling deployment step's settings to CrestApps Recipes.
/// </summary>
internal sealed class SchedulingDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(SchedulingDeploymentStep);

    protected override string DisplayText => "Scheduling";

    protected override string Description => "Exports appointment types and scheduling resources, and optionally the appointments and the waitlist.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("IncludeAppointments", DeploymentSchemaBuilders.Boolean("When true, exports every appointment too, with its customer's details but without its manage link."));
        yield return ("IncludeWaitlist", DeploymentSchemaBuilders.Boolean("When true, exports the waitlist too."));
    }
}
