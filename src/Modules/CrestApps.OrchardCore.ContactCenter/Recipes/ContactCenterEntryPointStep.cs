using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Deployments;
using CrestApps.OrchardCore.Core.Recipes;

namespace CrestApps.OrchardCore.ContactCenter.Recipes;

/// <summary>
/// Imports Contact Center entry points carried by a recipe step, creating entries that do not exist and updating those that
/// do.
/// </summary>
internal sealed class ContactCenterEntryPointStep : CatalogRecipeStep<ContactCenterEntryPoint>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterEntryPointStep"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the entry points.</param>
    public ContactCenterEntryPointStep(IContactCenterEntryPointManager manager)
        : base(ContactCenterDeploymentSteps.EntryPoint, "EntryPoints", manager)
    {
    }
}
