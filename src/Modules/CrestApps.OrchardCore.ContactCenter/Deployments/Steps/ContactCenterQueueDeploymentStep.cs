using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.ContactCenter.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports Contact Center queues.
/// </summary>
public sealed class ContactCenterQueueDeploymentStep : DeploymentStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterQueueDeploymentStep"/> class.
    /// </summary>
    public ContactCenterQueueDeploymentStep()
    {
        Name = ContactCenterDeploymentSteps.Queue;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterQueueDeploymentStep"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContactCenterQueueDeploymentStep(IStringLocalizer<ContactCenterQueueDeploymentStep> stringLocalizer)
        : this()
    {
        Category = stringLocalizer["Contact Center"];
    }
}
