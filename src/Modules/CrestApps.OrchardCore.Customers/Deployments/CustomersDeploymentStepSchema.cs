using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.Customers.Deployments;

/// <summary>
/// Describes the customers deployment step to CrestApps Recipes.
/// </summary>
internal sealed class CustomersDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(CustomersDeploymentStep);

    protected override string DisplayText => "Customers";

    protected override string Description => "Exports the guest and business customers with their addresses. Customers with a user account, and erased or merged customers, are left out.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield break;
    }
}
