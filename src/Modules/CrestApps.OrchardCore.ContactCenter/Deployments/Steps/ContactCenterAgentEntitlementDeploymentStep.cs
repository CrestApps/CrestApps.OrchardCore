using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.ContactCenter.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports manager-owned agent entitlements.
/// </summary>
public sealed class ContactCenterAgentEntitlementDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<ContactCenterAgentEntitlementDeploymentStep>("Contact Center");
    private static readonly LocalizationSource _title = LocalizationSource.Create<ContactCenterAgentEntitlementDeploymentStep>("Contact Center Agent Entitlements");

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterAgentEntitlementDeploymentStep"/> class.
    /// </summary>
    public ContactCenterAgentEntitlementDeploymentStep()
    {
        Name = ContactCenterDeploymentSteps.AgentEntitlement;
        Category = _category;
        Title = _title;
    }
}
