using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Deployments.Steps;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.ContactCenter.Deployments.Sources;

internal sealed class ContactCenterQueueGroupDeploymentSource : DeploymentSourceBase<ContactCenterQueueGroupDeploymentStep>
{
    private readonly IActivityQueueGroupManager _manager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterQueueGroupDeploymentSource"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the queue groups.</param>
    public ContactCenterQueueGroupDeploymentSource(IActivityQueueGroupManager manager)
    {
        _manager = manager;
    }

    protected override async Task ProcessAsync(ContactCenterQueueGroupDeploymentStep step, DeploymentPlanResult result)
    {
        var entries = await _manager.GetAllAsync();

        var data = new JsonArray();

        foreach (var entry in entries)
        {
            data.Add(ContactCenterDeploymentSerializer.Export(entry));
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = ContactCenterDeploymentSteps.QueueGroup,
            ["QueueGroups"] = data,
        });
    }
}
