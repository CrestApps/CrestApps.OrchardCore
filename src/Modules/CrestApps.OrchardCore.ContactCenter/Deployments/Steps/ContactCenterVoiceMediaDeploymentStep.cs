using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.ContactCenter.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports voice media clips.
/// </summary>
public sealed class ContactCenterVoiceMediaDeploymentStep : DeploymentStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterVoiceMediaDeploymentStep"/> class.
    /// </summary>
    public ContactCenterVoiceMediaDeploymentStep()
    {
        Name = ContactCenterDeploymentSteps.VoiceMedia;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterVoiceMediaDeploymentStep"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContactCenterVoiceMediaDeploymentStep(IStringLocalizer<ContactCenterVoiceMediaDeploymentStep> stringLocalizer)
        : this()
    {
        Category = stringLocalizer["Contact Center"];
    }
}
