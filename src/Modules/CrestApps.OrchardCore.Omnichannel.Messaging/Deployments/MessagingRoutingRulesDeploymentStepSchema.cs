using CrestApps.OrchardCore.Omnichannel.Messaging.Deployments.Steps;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Deployments;

/// <summary>
/// Describes the messaging routing rules deployment step.
/// </summary>
internal sealed class MessagingRoutingRulesDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    /// <inheritdoc/>
    public override string StepType => nameof(MessagingRoutingRulesDeploymentStep);

    /// <inheritdoc/>
    protected override string DisplayText => "Messaging routing rules";

    /// <inheritdoc/>
    protected override string Description => "Exports every messaging routing rule, in the order the rules are tried.";

    /// <inheritdoc/>
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield break;
    }
}
