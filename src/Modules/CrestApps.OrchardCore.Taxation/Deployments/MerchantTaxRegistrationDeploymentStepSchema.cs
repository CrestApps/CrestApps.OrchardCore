using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.Taxation.Deployments;

/// <summary>
/// Describes the tax registrations deployment step to CrestApps Recipes.
/// </summary>
internal sealed class MerchantTaxRegistrationDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(MerchantTaxRegistrationDeploymentStep);

    protected override string DisplayText => "Tax registrations";

    protected override string Description => "Exports the store's own tax registrations with their jurisdictions, numbers and thresholds.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield break;
    }
}
