using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Telephony.Deployments.Steps;
using CrestApps.OrchardCore.Telephony.Core.Services;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.Telephony.Deployments.Sources;

/// <summary>
/// Exports every stored entry of the telephony extensions catalog, whole, so a member added to the record later travels without a
/// change here.
/// </summary>
internal sealed class TelephonyExtensionDeploymentSource : DeploymentSourceBase<TelephonyExtensionDeploymentStep>
{
    private readonly ITelephonyExtensionManager _manager;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelephonyExtensionDeploymentSource"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the telephony extensions.</param>
    public TelephonyExtensionDeploymentSource(ITelephonyExtensionManager manager)
    {
        _manager = manager;
    }

    protected override async Task ProcessAsync(TelephonyExtensionDeploymentStep step, DeploymentPlanResult result)
    {
        var entries = await _manager.GetAllAsync();

        var data = new JsonArray();

        foreach (var entry in entries)
        {
            data.Add(CatalogDeploymentSerializer.Export(entry));
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = TelephonyDeploymentSteps.Extension,
            ["Extensions"] = data,
        });
    }
}
