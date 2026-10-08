using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Asterisk.Deployments.Steps;
using CrestApps.OrchardCore.Asterisk.Models;
using OrchardCore.Deployment;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Asterisk.Deployments.Sources;

/// <summary>
/// Exports the Asterisk provider settings. Every data-protected member is left out: it is encrypted with this tenant's keys, so no other
/// environment could read it, and a plan is not a place to keep a credential.
/// </summary>
internal sealed class AsteriskSettingsDeploymentSource : DeploymentSourceBase<AsteriskSettingsDeploymentStep>
{
    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsteriskSettingsDeploymentSource"/> class.
    /// </summary>
    /// <param name="siteService">The site service that stores the settings.</param>
    public AsteriskSettingsDeploymentSource(ISiteService siteService)
    {
        _siteService = siteService;
    }

    protected override async Task ProcessAsync(AsteriskSettingsDeploymentStep step, DeploymentPlanResult result)
    {
        var settings = await _siteService.GetSettingsAsync<AsteriskSettings>();

        result.Steps.Add(new JsonObject
        {
            ["name"] = AsteriskDeploymentSteps.Settings,
            ["Settings"] = ProtectedSettingsDeploymentSerializer.Export(settings, AsteriskDeploymentSteps.ProtectedMembers.Keys),
        });
    }
}
