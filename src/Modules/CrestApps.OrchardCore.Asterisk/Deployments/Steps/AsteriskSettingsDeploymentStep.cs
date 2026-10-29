using System.Text.Json.Serialization;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Asterisk.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports the Asterisk provider settings, without any of the secrets they hold.
/// </summary>
public sealed class AsteriskSettingsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<AsteriskSettingsDeploymentStep>("Telephony");
    private static readonly LocalizationSource _title = LocalizationSource.Create<AsteriskSettingsDeploymentStep>("Asterisk Settings");

    /// <inheritdoc />
    public override string Name => AsteriskDeploymentSteps.Settings;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}
