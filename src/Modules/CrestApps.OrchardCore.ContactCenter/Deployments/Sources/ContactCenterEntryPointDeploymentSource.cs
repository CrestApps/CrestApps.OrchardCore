using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Deployments.Steps;
using CrestApps.OrchardCore.Core.Deployments;

namespace CrestApps.OrchardCore.ContactCenter.Deployments.Sources;

/// <summary>
/// Exports every entry of the catalog as the <c>EntryPoint</c> recipe step.
/// </summary>
internal sealed class ContactCenterEntryPointDeploymentSource : CatalogDeploymentSource<ContactCenterEntryPointDeploymentStep, ContactCenterEntryPoint>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterEntryPointDeploymentSource"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the entry points.</param>
    public ContactCenterEntryPointDeploymentSource(IContactCenterEntryPointManager manager)
        : base("EntryPoints", manager)
    {
    }
}
