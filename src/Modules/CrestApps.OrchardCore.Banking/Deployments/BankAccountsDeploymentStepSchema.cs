using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.Banking.Deployments;

internal sealed class BankAccountsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(BankAccountsDeploymentStep);

    protected override string DisplayText => "Bank accounts";

    protected override string Description => "Exports every bank account, without its routing and account numbers unless IncludeSensitiveData is on.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        return [("IncludeSensitiveData", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the routing and account numbers and the status are exported, in clear text."))];
    }
}
