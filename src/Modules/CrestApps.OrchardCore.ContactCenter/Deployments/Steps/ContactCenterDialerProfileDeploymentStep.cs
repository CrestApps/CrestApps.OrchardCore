using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.ContactCenter.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports Contact Center dialer profiles.
/// </summary>
public sealed class ContactCenterDialerProfileDeploymentStep : DeploymentStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterDialerProfileDeploymentStep"/> class.
    /// </summary>
    public ContactCenterDialerProfileDeploymentStep()
    {
        Name = ContactCenterDeploymentSteps.DialerProfile;
        Category = LocalizationSource.Create<ContactCenterDialerProfileDeploymentStep>("Contact Center");
    }
}
