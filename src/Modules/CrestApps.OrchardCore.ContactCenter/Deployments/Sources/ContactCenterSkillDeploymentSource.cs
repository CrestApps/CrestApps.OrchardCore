using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Deployments.Steps;
using CrestApps.OrchardCore.Core.Deployments;

namespace CrestApps.OrchardCore.ContactCenter.Deployments.Sources;

/// <summary>
/// Exports every entry of the catalog as the <c>Skill</c> recipe step.
/// </summary>
internal sealed class ContactCenterSkillDeploymentSource : CatalogDeploymentSource<ContactCenterSkillDeploymentStep, ContactCenterSkill>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterSkillDeploymentSource"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the skills.</param>
    public ContactCenterSkillDeploymentSource(IContactCenterSkillManager manager)
        : base("Skills", manager)
    {
    }
}
