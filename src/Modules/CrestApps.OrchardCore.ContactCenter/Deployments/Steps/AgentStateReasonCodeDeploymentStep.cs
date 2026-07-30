using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.ContactCenter.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports Contact Center agent state reason codes.
/// </summary>
public sealed class AgentStateReasonCodeDeploymentStep : DeploymentStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AgentStateReasonCodeDeploymentStep"/> class.
    /// </summary>
    public AgentStateReasonCodeDeploymentStep()
    {
        Name = ContactCenterDeploymentSteps.AgentStateReasonCode;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentStateReasonCodeDeploymentStep"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AgentStateReasonCodeDeploymentStep(IStringLocalizer<AgentStateReasonCodeDeploymentStep> stringLocalizer)
        : this()
    {
        Category = stringLocalizer["Contact Center"];
    }
}
