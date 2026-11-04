using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.Customers.Deployments;

/// <summary>
/// Describes the referral codes deployment step to CrestApps Recipes.
/// </summary>
internal sealed class ReferralProfilesDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(ReferralProfilesDeploymentStep);

    protected override string DisplayText => "Referral codes";

    protected override string Description => "Exports every customer's referral code, with the referrals it earned.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield break;
    }
}
