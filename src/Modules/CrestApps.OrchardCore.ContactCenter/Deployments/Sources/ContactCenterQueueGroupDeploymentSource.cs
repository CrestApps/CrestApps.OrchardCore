using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Deployments.Steps;
using CrestApps.OrchardCore.Core.Deployments;

namespace CrestApps.OrchardCore.ContactCenter.Deployments.Sources;

/// <summary>
/// Exports every entry of the catalog as the <c>QueueGroup</c> recipe step.
/// </summary>
internal sealed class ContactCenterQueueGroupDeploymentSource : CatalogDeploymentSource<ContactCenterQueueGroupDeploymentStep, ActivityQueueGroup>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterQueueGroupDeploymentSource"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the queue groups.</param>
    public ContactCenterQueueGroupDeploymentSource(IActivityQueueGroupManager manager)
        : base("QueueGroups", manager)
    {
    }
}
