using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Deployments;
using CrestApps.OrchardCore.Core.Recipes;

namespace CrestApps.OrchardCore.ContactCenter.Recipes;

/// <summary>
/// Imports Contact Center skills carried by a recipe step, creating entries that do not exist and updating those that
/// do.
/// </summary>
internal sealed class ContactCenterSkillStep : CatalogRecipeStep<ContactCenterSkill>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterSkillStep"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the skills.</param>
    public ContactCenterSkillStep(IContactCenterSkillManager manager)
        : base(ContactCenterDeploymentSteps.Skill, "Skills", manager)
    {
    }
}
