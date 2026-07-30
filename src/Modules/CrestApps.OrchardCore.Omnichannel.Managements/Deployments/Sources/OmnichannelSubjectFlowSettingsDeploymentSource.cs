using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Steps;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Sources;

internal sealed class OmnichannelSubjectFlowSettingsDeploymentSource : DeploymentSourceBase<OmnichannelSubjectFlowSettingsDeploymentStep>
{
    private readonly ICatalogManager<SubjectFlowSettings> _manager;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelSubjectFlowSettingsDeploymentSource"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the subject flow settings.</param>
    public OmnichannelSubjectFlowSettingsDeploymentSource(ICatalogManager<SubjectFlowSettings> manager)
    {
        _manager = manager;
    }

    protected override async Task ProcessAsync(OmnichannelSubjectFlowSettingsDeploymentStep step, DeploymentPlanResult result)
    {
        var entries = await _manager.GetAllAsync();

        var data = new JsonArray();

        foreach (var entry in entries)
        {
            data.Add(JsonSerializer.SerializeToNode(entry, entry.GetType(), OmnichannelDeploymentSerializer.Options));
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = OmnichannelDeploymentSteps.SubjectFlowSettings,
            ["SubjectFlows"] = data,
        });
    }
}
