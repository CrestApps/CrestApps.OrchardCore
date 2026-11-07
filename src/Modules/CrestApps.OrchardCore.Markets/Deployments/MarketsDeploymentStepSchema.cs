using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.Markets.Deployments;

/// <summary>
/// Describes the markets deployment step to CrestApps Recipes.
/// </summary>
internal sealed class MarketsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(MarketsDeploymentStep);

    protected override string DisplayText => "Markets";

    protected override string Description => "Exports every market with its countries, currency, exchange rate and rounding.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield break;
    }
}
