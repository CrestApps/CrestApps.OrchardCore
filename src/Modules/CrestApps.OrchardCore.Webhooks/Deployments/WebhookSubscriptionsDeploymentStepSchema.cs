using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.Webhooks.Deployments;

/// <summary>
/// Describes the Webhook subscriptions deployment step's settings to CrestApps Recipes.
/// </summary>
internal sealed class WebhookSubscriptionsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(WebhookSubscriptionsDeploymentStep);

    protected override string DisplayText => "Webhook subscriptions";

    protected override string Description => "Exports every webhook subscription without its signing secret.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
        => [];
}
