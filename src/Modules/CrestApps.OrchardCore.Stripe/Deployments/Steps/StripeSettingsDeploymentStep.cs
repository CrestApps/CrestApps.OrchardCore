using System.Text.Json.Serialization;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Stripe.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports the Stripe settings, without any of the secrets they hold.
/// </summary>
public sealed class StripeSettingsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<StripeSettingsDeploymentStep>("Payments");
    private static readonly LocalizationSource _title = LocalizationSource.Create<StripeSettingsDeploymentStep>("Stripe Settings");

    /// <inheritdoc />
    public override string Name => StripeDeploymentSteps.Settings;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}
