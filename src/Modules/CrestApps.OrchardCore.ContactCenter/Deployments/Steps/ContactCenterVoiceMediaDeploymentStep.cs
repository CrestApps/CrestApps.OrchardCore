using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.ContactCenter.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports voice media clips.
/// </summary>
public sealed class ContactCenterVoiceMediaDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<ContactCenterVoiceMediaDeploymentStep>("Contact Center");
    private static readonly LocalizationSource _title = LocalizationSource.Create<ContactCenterVoiceMediaDeploymentStep>("Contact Center Voice Media Library");

    /// <inheritdoc />
    public override string Name => ContactCenterDeploymentSteps.VoiceMedia;

    /// <inheritdoc />
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    public override LocalizationSource Title => _title;
}
