using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.ContactCenter.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports manager-owned agent entitlements.
/// </summary>
public sealed class ContactCenterAgentEntitlementDeploymentStep : DeploymentStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterAgentEntitlementDeploymentStep"/> class.
    /// </summary>
    public ContactCenterAgentEntitlementDeploymentStep()
    {
        Name = ContactCenterDeploymentSteps.AgentEntitlement;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterAgentEntitlementDeploymentStep"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContactCenterAgentEntitlementDeploymentStep(IStringLocalizer<ContactCenterAgentEntitlementDeploymentStep> stringLocalizer)
        : this()
    {
        Category = stringLocalizer["Contact Center"];
    }
}
