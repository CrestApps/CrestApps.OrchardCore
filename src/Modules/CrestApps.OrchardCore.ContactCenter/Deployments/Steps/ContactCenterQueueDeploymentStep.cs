using OrchardCore.Deployment;
using OrchardCore.Localization;

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
        Category = LocalizationSource.Create<ContactCenterQueueDeploymentStep>("Contact Center");
    }
}
