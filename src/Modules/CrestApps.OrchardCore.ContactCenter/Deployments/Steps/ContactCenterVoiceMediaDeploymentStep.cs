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

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterVoiceMediaDeploymentStep"/> class.
    /// </summary>
    public ContactCenterVoiceMediaDeploymentStep()
    {
        Name = ContactCenterDeploymentSteps.VoiceMedia;
        Category = _category;
        Title = _title;
    }
}
