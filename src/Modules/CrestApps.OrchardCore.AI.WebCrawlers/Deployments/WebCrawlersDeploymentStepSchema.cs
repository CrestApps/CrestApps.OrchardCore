using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.AI.WebCrawlers.Deployments;

/// <summary>
/// Describes the web crawlers deployment step to CrestApps Recipes.
/// </summary>
internal sealed class WebCrawlersDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(WebCrawlersDeploymentStep);

    protected override string DisplayText => "Web crawlers";

    protected override string Description => "Exports every web crawler with its crawl settings. Secrets are never exported.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield break;
    }
}
