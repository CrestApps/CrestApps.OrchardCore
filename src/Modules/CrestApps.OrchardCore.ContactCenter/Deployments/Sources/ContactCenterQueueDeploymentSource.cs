using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Deployments.Steps;
using CrestApps.OrchardCore.Core.Deployments;

namespace CrestApps.OrchardCore.ContactCenter.Deployments.Sources;

/// <summary>
/// Exports every entry of the catalog as the <c>Queue</c> recipe step.
/// </summary>
internal sealed class ContactCenterQueueDeploymentSource : CatalogDeploymentSource<ContactCenterQueueDeploymentStep, ActivityQueue>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterQueueDeploymentSource"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the queues.</param>
    public ContactCenterQueueDeploymentSource(IActivityQueueManager manager)
        : base("Queues", manager)
    {
    }
}
