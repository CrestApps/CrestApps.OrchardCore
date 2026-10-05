using OrchardCore.Deployment;
using OrchardCore.Localization;

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
        Category = LocalizationSource.Create<ContactCenterQueueGroupDeploymentStep>("Contact Center");
    }
}
