using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Deployments.Steps;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.ContactCenter.Deployments.Sources;

internal sealed class ContactCenterDialerProfileDeploymentSource : DeploymentSourceBase<ContactCenterDialerProfileDeploymentStep>
{
    private readonly IDialerProfileManager _manager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterDialerProfileDeploymentSource"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the dialer profiles.</param>
    public ContactCenterDialerProfileDeploymentSource(IDialerProfileManager manager)
    {
        _manager = manager;
    }

    protected override async Task ProcessAsync(ContactCenterDialerProfileDeploymentStep step, DeploymentPlanResult result)
    {
        var entries = await _manager.GetAllAsync();

        var data = new JsonArray();

        foreach (var entry in entries)
        {
            data.Add(ContactCenterDeploymentSerializer.Export(entry));
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = ContactCenterDeploymentSteps.DialerProfile,
            ["DialerProfiles"] = data,
        });
    }
}
