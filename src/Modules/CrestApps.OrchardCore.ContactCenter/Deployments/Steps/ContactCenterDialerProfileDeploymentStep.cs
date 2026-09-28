using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;

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
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterDialerProfileDeploymentStep"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContactCenterDialerProfileDeploymentStep(IStringLocalizer<ContactCenterDialerProfileDeploymentStep> stringLocalizer)
        : this()
    {
        Category = stringLocalizer["Contact Center"];
    }
}
