using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.ContactCenter.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports Contact Center skills.
/// </summary>
public sealed class ContactCenterSkillDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<ContactCenterSkillDeploymentStep>("Contact Center");
    private static readonly LocalizationSource _title = LocalizationSource.Create<ContactCenterSkillDeploymentStep>("Contact Center Skills");

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterSkillDeploymentStep"/> class.
    /// </summary>
    public ContactCenterSkillDeploymentStep()
    {
        Name = ContactCenterDeploymentSteps.Skill;
        Category = _category;
        Title = _title;
    }
}
