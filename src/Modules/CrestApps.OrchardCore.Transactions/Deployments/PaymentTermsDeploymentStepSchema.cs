using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.Transactions.Deployments;

/// <summary>
/// Describes the payment terms deployment step to CrestApps Recipes.
/// </summary>
internal sealed class PaymentTermsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(PaymentTermsDeploymentStep);

    protected override string DisplayText => "Payment terms";

    protected override string Description => "Exports every payment term of the store.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
        => [];
}
