using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Deployments;
using CrestApps.OrchardCore.Core.Recipes;

namespace CrestApps.OrchardCore.ContactCenter.Recipes;

/// <summary>
/// Imports Contact Center queues carried by a recipe step, creating entries that do not exist and updating those that
/// do.
/// </summary>
internal sealed class ContactCenterQueueStep : CatalogRecipeStep<ActivityQueue>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterQueueStep"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the queues.</param>
    public ContactCenterQueueStep(IActivityQueueManager manager)
        : base(ContactCenterDeploymentSteps.Queue, "Queues", manager)
    {
    }
}
