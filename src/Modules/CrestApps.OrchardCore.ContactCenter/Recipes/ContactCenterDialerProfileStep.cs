using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Deployments;
using CrestApps.OrchardCore.Core.Recipes;

namespace CrestApps.OrchardCore.ContactCenter.Recipes;

/// <summary>
/// Imports Contact Center dialer profiles carried by a recipe step, creating entries that do not exist and updating those that
/// do.
/// </summary>
internal sealed class ContactCenterDialerProfileStep : CatalogRecipeStep<DialerProfile>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterDialerProfileStep"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the dialer profiles.</param>
    public ContactCenterDialerProfileStep(IDialerProfileManager manager)
        : base(ContactCenterDeploymentSteps.DialerProfile, "DialerProfiles", manager)
    {
    }
}
