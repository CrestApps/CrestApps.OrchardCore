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

    /// <inheritdoc />
    public override string Name => ContactCenterDeploymentSteps.AgentEntitlement;

    /// <inheritdoc />
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Title => _title;
}
