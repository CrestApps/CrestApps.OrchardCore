using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Deployments.Steps;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.ContactCenter.Deployments.Sources;

internal sealed class AgentStateReasonCodeDeploymentSource : DeploymentSourceBase<AgentStateReasonCodeDeploymentStep>
{
    private readonly IAgentStateReasonCodeManager _manager;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentStateReasonCodeDeploymentSource"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the agent state reason codes.</param>
    public AgentStateReasonCodeDeploymentSource(IAgentStateReasonCodeManager manager)
    {
        _manager = manager;
    }

    protected override async Task ProcessAsync(AgentStateReasonCodeDeploymentStep step, DeploymentPlanResult result)
    {
        var entries = await _manager.GetAllAsync();

        var data = new JsonArray();

        foreach (var entry in entries)
        {
            data.Add(ContactCenterDeploymentSerializer.Export(entry));
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = ContactCenterDeploymentSteps.AgentStateReasonCode,
            ["ReasonCodes"] = data,
        });
    }
}
