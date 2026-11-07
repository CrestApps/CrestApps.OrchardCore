using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Deployments;
using CrestApps.OrchardCore.Core.Recipes;

namespace CrestApps.OrchardCore.ContactCenter.Recipes;

/// <summary>
/// Imports Contact Center queue groups carried by a recipe step, creating entries that do not exist and updating those that
/// do.
/// </summary>
internal sealed class ContactCenterQueueGroupStep : CatalogRecipeStep<ActivityQueueGroup>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterQueueGroupStep"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the queue groups.</param>
    public ContactCenterQueueGroupStep(IActivityQueueGroupManager manager)
        : base(ContactCenterDeploymentSteps.QueueGroup, "QueueGroups", manager)
    {
    }
}
