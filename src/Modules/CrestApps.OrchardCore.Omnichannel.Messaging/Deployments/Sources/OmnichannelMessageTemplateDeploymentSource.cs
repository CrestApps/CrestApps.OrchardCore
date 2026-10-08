using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Omnichannel.Messaging.Deployments.Steps;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Deployments.Sources;

/// <summary>
/// Exports every stored entry of the messaging templates catalog, whole, so a member added to the record later travels without a
/// change here.
/// </summary>
internal sealed class OmnichannelMessageTemplateDeploymentSource : DeploymentSourceBase<OmnichannelMessageTemplateDeploymentStep>
{
    private readonly IMessageTemplateManager _manager;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelMessageTemplateDeploymentSource"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the messaging templates.</param>
    public OmnichannelMessageTemplateDeploymentSource(IMessageTemplateManager manager)
    {
        _manager = manager;
    }

    protected override async Task ProcessAsync(OmnichannelMessageTemplateDeploymentStep step, DeploymentPlanResult result)
    {
        var entries = await _manager.GetAllAsync();

        var data = new JsonArray();

        foreach (var entry in entries)
        {
            data.Add(CatalogDeploymentSerializer.Export(entry));
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = MessagingDeploymentSteps.MessageTemplate,
            ["Templates"] = data,
        });
    }
}
