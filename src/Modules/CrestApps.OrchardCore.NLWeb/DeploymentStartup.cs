using CrestApps.OrchardCore.NLWeb.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Deployment;
using OrchardCore.Modules;
using OrchardCore.Settings.Deployment;

namespace CrestApps.OrchardCore.NLWeb;

/// <summary>
/// Exports the NLWeb settings with a deployment step; Orchard Core's <c>Settings</c> recipe step imports them.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class DeploymentStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteSettingsPropertyDeploymentStep<NLWebSettings, DeploymentStartup>(
            S => S["NLWeb settings"],
            S => S["What the NLWeb endpoint searches and returns: the indexes, content types, result count, modes, and the AI deployment and data source it uses."]);
    }
}
