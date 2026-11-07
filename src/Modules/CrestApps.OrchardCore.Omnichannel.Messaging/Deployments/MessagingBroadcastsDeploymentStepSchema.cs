using CrestApps.OrchardCore.Omnichannel.Messaging.Deployments.Steps;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Deployments;

/// <summary>
/// Describes the messaging broadcasts deployment step to CrestApps Recipes.
/// </summary>
internal sealed class MessagingBroadcastsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(MessagingBroadcastsDeploymentStep);

    protected override string DisplayText => "Messaging broadcasts";

    protected override string Description => "Exports every broadcast with its message and recipients, without its status or counts. Broadcasts import as drafts, so nothing is sent.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield break;
    }
}
