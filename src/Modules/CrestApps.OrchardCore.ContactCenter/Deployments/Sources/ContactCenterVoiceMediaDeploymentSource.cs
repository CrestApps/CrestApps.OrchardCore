using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContactCenter.Deployments.Steps;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.ContactCenter.Deployments.Sources;

/// <summary>
/// Exports every stored entry of the voice media clips catalog, whole, so a member added to the record later travels without a
/// change here.
/// </summary>
internal sealed class ContactCenterVoiceMediaDeploymentSource : DeploymentSourceBase<ContactCenterVoiceMediaDeploymentStep>
{
    private readonly IVoiceMediaItemManager _manager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterVoiceMediaDeploymentSource"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the voice media clips.</param>
    public ContactCenterVoiceMediaDeploymentSource(IVoiceMediaItemManager manager)
    {
        _manager = manager;
    }

    protected override async Task ProcessAsync(ContactCenterVoiceMediaDeploymentStep step, DeploymentPlanResult result)
    {
        var entries = await _manager.GetAllAsync();

        var data = new JsonArray();

        foreach (var entry in entries)
        {
            data.Add(ContactCenterDeploymentSerializer.Export(entry));
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = ContactCenterDeploymentSteps.VoiceMedia,
            ["VoiceMedia"] = data,
        });
    }
}
