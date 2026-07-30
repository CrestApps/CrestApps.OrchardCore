using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Deployments.Steps;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.ContactCenter.Deployments.Sources;

internal sealed class ContactCenterQueueDeploymentSource : DeploymentSourceBase<ContactCenterQueueDeploymentStep>
{
    private readonly IActivityQueueManager _manager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterQueueDeploymentSource"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the queues.</param>
    public ContactCenterQueueDeploymentSource(IActivityQueueManager manager)
    {
        _manager = manager;
    }

    protected override async Task ProcessAsync(ContactCenterQueueDeploymentStep step, DeploymentPlanResult result)
    {
        var entries = await _manager.GetAllAsync();

        var data = new JsonArray();

        foreach (var entry in entries)
        {
            data.Add(ContactCenterDeploymentSerializer.Export(entry));
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = ContactCenterDeploymentSteps.Queue,
            ["Queues"] = data,
        });
    }
}
