using OrchardCore.Deployment;
using OrchardCore.Localization;

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
        Category = LocalizationSource.Create<ContactCenterAgentEntitlementDeploymentStep>("Contact Center");
    }
}
