using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.ContactCenter.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports Contact Center skills.
/// </summary>
public sealed class ContactCenterSkillDeploymentStep : DeploymentStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterSkillDeploymentStep"/> class.
    /// </summary>
    public ContactCenterSkillDeploymentStep()
    {
        Name = ContactCenterDeploymentSteps.Skill;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterSkillDeploymentStep"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContactCenterSkillDeploymentStep(IStringLocalizer<ContactCenterSkillDeploymentStep> stringLocalizer)
        : this()
    {
        Category = stringLocalizer["Contact Center"];
    }
}
