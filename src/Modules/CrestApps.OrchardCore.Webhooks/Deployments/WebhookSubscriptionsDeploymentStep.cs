using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Webhooks.Deployments;

/// <summary>
/// Exports every webhook subscription as a <c>WebhookSubscriptions</c> recipe step. Signing secrets are not exported:
/// they are protected with the site's own keys, so the importing site makes its own unless the recipe carries one.
/// </summary>
public sealed class WebhookSubscriptionsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<WebhookSubscriptionsDeploymentStep>("Webhooks");
    private static readonly LocalizationSource _title = LocalizationSource.Create<WebhookSubscriptionsDeploymentStep>("Webhook subscriptions");

    /// <inheritdoc/>
    public override string Name => nameof(WebhookSubscriptionsDeploymentStep);

    /// <inheritdoc/>
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc/>
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Title => _title;
}
