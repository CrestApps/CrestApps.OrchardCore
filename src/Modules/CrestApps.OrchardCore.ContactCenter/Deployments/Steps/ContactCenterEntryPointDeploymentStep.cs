using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.ContactCenter.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports Contact Center entry points.
/// </summary>
public sealed class ContactCenterEntryPointDeploymentStep : DeploymentStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterEntryPointDeploymentStep"/> class.
    /// </summary>
    public ContactCenterEntryPointDeploymentStep()
    {
        Name = ContactCenterDeploymentSteps.EntryPoint;
        Category = LocalizationSource.Create<ContactCenterEntryPointDeploymentStep>("Contact Center");
    }
}
