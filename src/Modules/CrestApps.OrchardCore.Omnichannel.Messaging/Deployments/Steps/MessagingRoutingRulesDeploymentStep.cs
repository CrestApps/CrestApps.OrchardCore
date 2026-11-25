using CrestApps.OrchardCore.Omnichannel.Messaging.Recipes;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Deployments.Steps;

/// <summary>
/// A deployment step that exports every messaging routing rule.
/// </summary>
public sealed class MessagingRoutingRulesDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<MessagingRoutingRulesDeploymentStep>("Omnichannel");
    private static readonly LocalizationSource _title = LocalizationSource.Create<MessagingRoutingRulesDeploymentStep>("Messaging routing rules");

    /// <inheritdoc/>
    public override string Name => MessagingRoutingRulesStep.StepKey;

    /// <inheritdoc/>
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc/>
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Title => _title;
}
