using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Deployments.Steps;
using CrestApps.OrchardCore.Core.Deployments;

namespace CrestApps.OrchardCore.ContactCenter.Deployments.Sources;

/// <summary>
/// Exports every entry of the catalog as the <c>DialerProfile</c> recipe step.
/// </summary>
internal sealed class ContactCenterDialerProfileDeploymentSource : CatalogDeploymentSource<ContactCenterDialerProfileDeploymentStep, DialerProfile>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterDialerProfileDeploymentSource"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the dialer profiles.</param>
    public ContactCenterDialerProfileDeploymentSource(IDialerProfileManager manager)
        : base("DialerProfiles", manager)
    {
    }
}
