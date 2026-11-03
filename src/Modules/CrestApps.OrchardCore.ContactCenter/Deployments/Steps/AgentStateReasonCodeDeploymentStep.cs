using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.ContactCenter.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports Contact Center agent state reason codes.
/// </summary>
public sealed class AgentStateReasonCodeDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<AgentStateReasonCodeDeploymentStep>("Contact Center");
    private static readonly LocalizationSource _title = LocalizationSource.Create<AgentStateReasonCodeDeploymentStep>("Agent State Reason Codes");

    /// <inheritdoc />
    public override string Name => ContactCenterDeploymentSteps.AgentStateReasonCode;

    /// <inheritdoc />
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Title => _title;
}
