using CrestApps.OrchardCore.Dispatch.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Deployment;
using OrchardCore.Modules;
using OrchardCore.Settings.Deployment;

namespace CrestApps.OrchardCore.Dispatch;

/// <summary>
/// Exports the Dispatch settings with a deployment; Orchard Core's Settings recipe step imports them.
/// </summary>
[RequireFeatures("OrchardCore.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteSettingsPropertyDeploymentStep<DispatchSettings, DeploymentStartup>(
            S => S["Dispatch settings"],
            S => S["How visits are scheduled and dispatched: the objective, arrival windows, travel, customer notices and visits at risk."]);
    }
}
