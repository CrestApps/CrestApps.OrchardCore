using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Deployments.Steps;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.ContactCenter.Deployments.Sources;

internal sealed class ContactCenterSkillDeploymentSource : DeploymentSourceBase<ContactCenterSkillDeploymentStep>
{
    private readonly IContactCenterSkillManager _manager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterSkillDeploymentSource"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the skills.</param>
    public ContactCenterSkillDeploymentSource(IContactCenterSkillManager manager)
    {
        _manager = manager;
    }

    protected override async Task ProcessAsync(ContactCenterSkillDeploymentStep step, DeploymentPlanResult result)
    {
        var entries = await _manager.GetAllAsync();

        var data = new JsonArray();

        foreach (var entry in entries)
        {
            data.Add(ContactCenterDeploymentSerializer.Export(entry));
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = ContactCenterDeploymentSteps.Skill,
            ["Skills"] = data,
        });
    }
}
