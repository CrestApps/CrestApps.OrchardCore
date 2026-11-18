using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.Agreements.Deployments;

/// <summary>
/// Describes the agreements deployment step.
/// </summary>
internal sealed class AgreementsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(AgreementsDeploymentStep);

    protected override string DisplayText => "Agreements";

    protected override string Description => "Exports every agreement with accounts.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
        => [];
}
