using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.ContactCenter.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports Contact Center queue groups.
/// </summary>
public sealed class ContactCenterQueueGroupDeploymentStep : DeploymentStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterQueueGroupDeploymentStep"/> class.
    /// </summary>
    public ContactCenterQueueGroupDeploymentStep()
    {
        Name = ContactCenterDeploymentSteps.QueueGroup;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterQueueGroupDeploymentStep"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContactCenterQueueGroupDeploymentStep(IStringLocalizer<ContactCenterQueueGroupDeploymentStep> stringLocalizer)
        : this()
    {
        Category = stringLocalizer["Contact Center"];
    }
}
