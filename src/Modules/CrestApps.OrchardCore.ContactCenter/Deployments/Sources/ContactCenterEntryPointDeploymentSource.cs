using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Deployments.Steps;
using CrestApps.OrchardCore.Core.Deployments;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.ContactCenter.Deployments.Sources;

internal sealed class ContactCenterEntryPointDeploymentSource : DeploymentSourceBase<ContactCenterEntryPointDeploymentStep>
{
    private readonly IContactCenterEntryPointManager _manager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterEntryPointDeploymentSource"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the entry points.</param>
    public ContactCenterEntryPointDeploymentSource(IContactCenterEntryPointManager manager)
    {
        _manager = manager;
    }

    protected override async Task ProcessAsync(ContactCenterEntryPointDeploymentStep step, DeploymentPlanResult result)
    {
        var entries = await _manager.GetAllAsync();

        var data = new JsonArray();

        foreach (var entry in entries)
        {
            data.Add(CatalogDeploymentSerializer.Export(entry));
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = ContactCenterDeploymentSteps.EntryPoint,
            ["EntryPoints"] = data,
        });
    }
}
