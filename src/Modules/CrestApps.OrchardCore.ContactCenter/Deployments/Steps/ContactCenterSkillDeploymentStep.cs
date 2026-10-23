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

    /// <inheritdoc />
    public override string Name => ContactCenterDeploymentSteps.Skill;

    /// <inheritdoc />
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    public override LocalizationSource Title => _title;
}
