using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Stripe.Deployments.Steps;
using CrestApps.OrchardCore.Stripe.Models;
using OrchardCore.Deployment;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Stripe.Deployments.Sources;

/// <summary>
/// Exports the Stripe settings stored on the settings page. Every data-protected member is left out: it is encrypted
/// with this tenant's keys, so no other environment could read it, and a plan is not a place to keep a credential. The
/// verified account and webhook identifiers are left out too, since they describe this site's connection to Stripe.
/// </summary>
/// <remarks>
/// Only the stored settings are exported. Values supplied by the application configuration are not part of the plan;
/// each environment supplies its own.
/// </remarks>
internal sealed class StripeSettingsDeploymentSource : DeploymentSourceBase<StripeSettingsDeploymentStep>
{
    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="StripeSettingsDeploymentSource"/> class.
    /// </summary>
    /// <param name="siteService">The site service that stores the settings.</param>
    public StripeSettingsDeploymentSource(ISiteService siteService)
    {
        _siteService = siteService;
    }

    protected override async Task ProcessAsync(StripeSettingsDeploymentStep step, DeploymentPlanResult result)
    {
        var settings = await _siteService.GetSettingsAsync<StripeSettings>();

        result.Steps.Add(new JsonObject
        {
            ["name"] = StripeDeploymentSteps.Settings,
            ["Settings"] = ProtectedSettingsDeploymentSerializer.Export(
                settings,
                StripeDeploymentSteps.ProtectedMembers.Keys.Concat(StripeDeploymentSteps.EnvironmentOwnedMembers)),
        });
    }
}
