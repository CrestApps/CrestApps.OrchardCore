using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Telnyx.Deployments.Steps;
using CrestApps.OrchardCore.Telnyx.Models;
using OrchardCore.Deployment;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Telnyx.Deployments.Sources;

/// <summary>
/// Exports the Telnyx SMS provider settings. Every data-protected member is left out: it is encrypted with this tenant's keys, so no other
/// environment could read it, and a plan is not a place to keep a credential.
/// </summary>
internal sealed class TelnyxSmsSettingsDeploymentSource : DeploymentSourceBase<TelnyxSmsSettingsDeploymentStep>
{
    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxSmsSettingsDeploymentSource"/> class.
    /// </summary>
    /// <param name="siteService">The site service that stores the settings.</param>
    public TelnyxSmsSettingsDeploymentSource(ISiteService siteService)
    {
        _siteService = siteService;
    }

    protected override async Task ProcessAsync(TelnyxSmsSettingsDeploymentStep step, DeploymentPlanResult result)
    {
        var settings = await _siteService.GetSettingsAsync<TelnyxSmsSettings>();

        result.Steps.Add(new JsonObject
        {
            ["name"] = TelnyxDeploymentSteps.SmsSettings,
            ["Settings"] = ProtectedSettingsDeploymentSerializer.Export(settings, TelnyxDeploymentSteps.SmsProtectedMembers.Keys),
        });
    }
}
