using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.ContactCenter.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports Contact Center dialer profiles.
/// </summary>
public sealed class ContactCenterDialerProfileDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<ContactCenterDialerProfileDeploymentStep>("Contact Center");
    private static readonly LocalizationSource _title = LocalizationSource.Create<ContactCenterDialerProfileDeploymentStep>("Contact Center Dialer Profiles");

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterDialerProfileDeploymentStep"/> class.
    /// </summary>
    public ContactCenterDialerProfileDeploymentStep()
    {
        Name = ContactCenterDeploymentSteps.DialerProfile;
        Category = _category;
        Title = _title;
    }
}
