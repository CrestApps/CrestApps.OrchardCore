using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Deployments.Steps;
using CrestApps.OrchardCore.Core.Deployments;

namespace CrestApps.OrchardCore.ContactCenter.Deployments.Sources;

/// <summary>
/// Exports every entry of the catalog as the <c>AgentStateReasonCode</c> recipe step.
/// </summary>
internal sealed class AgentStateReasonCodeDeploymentSource : CatalogDeploymentSource<AgentStateReasonCodeDeploymentStep, AgentStateReasonCode>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AgentStateReasonCodeDeploymentSource"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the agent state reason codes.</param>
    public AgentStateReasonCodeDeploymentSource(IAgentStateReasonCodeManager manager)
        : base("ReasonCodes", manager)
    {
    }
}
