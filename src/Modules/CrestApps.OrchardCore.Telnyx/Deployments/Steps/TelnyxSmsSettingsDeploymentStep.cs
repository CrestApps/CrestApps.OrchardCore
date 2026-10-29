using System.Text.Json.Serialization;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Telnyx.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports the Telnyx SMS provider settings, without any of the secrets they hold.
/// </summary>
public sealed class TelnyxSmsSettingsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<TelnyxSmsSettingsDeploymentStep>("Telephony");
    private static readonly LocalizationSource _title = LocalizationSource.Create<TelnyxSmsSettingsDeploymentStep>("Telnyx SMS Settings");

    /// <inheritdoc />
    public override string Name => TelnyxDeploymentSteps.SmsSettings;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}
