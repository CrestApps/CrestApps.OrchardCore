using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.Marketing.Deployments;

/// <summary>
/// Describes the marketing segments and campaigns deployment step to CrestApps Recipes.
/// </summary>
internal sealed class MarketingDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(MarketingDeploymentStep);

    protected override string DisplayText => "Marketing segments and campaigns";

    protected override string Description => "Exports every marketing segment and campaign. Campaigns are exported without their status and counts, and import as drafts.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield break;
    }
}
