using CrestApps.OrchardCore.Omnichannel.Messaging.Recipes;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Deployments.Steps;

/// <summary>
/// Exports every messaging broadcast, without its delivery, as a <c>MessagingBroadcasts</c> recipe step. Broadcasts
/// import as drafts, so a deployment never sends a message. Recipients are personal data.
/// </summary>
public sealed class MessagingBroadcastsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<MessagingBroadcastsDeploymentStep>("Omnichannel");
    private static readonly LocalizationSource _title = LocalizationSource.Create<MessagingBroadcastsDeploymentStep>("Messaging broadcasts");

    /// <inheritdoc/>
    public override string Name => MessagingBroadcastsStep.StepKey;

    /// <inheritdoc/>
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc/>
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Title => _title;
}
