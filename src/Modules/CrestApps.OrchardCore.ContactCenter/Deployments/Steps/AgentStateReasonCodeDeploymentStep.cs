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

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentStateReasonCodeDeploymentStep"/> class.
    /// </summary>
    public AgentStateReasonCodeDeploymentStep()
    {
        Name = ContactCenterDeploymentSteps.AgentStateReasonCode;
        Category = _category;
        Title = _title;
    }
}
