using CrestApps.Core.AI.Memory;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Deployment;
using OrchardCore.Modules;
using OrchardCore.Settings.Deployment;

namespace CrestApps.OrchardCore.AI.Memory;

/// <summary>
/// Exports the AI memory settings with a deployment step; Orchard Core's <c>Settings</c> recipe step imports them.
/// </summary>
[Feature(MemoryConstants.Feature.Memory)]
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class DeploymentStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteSettingsPropertyDeploymentStep<AIMemorySettings, DeploymentStartup>(
            S => S["AI memory settings"],
            S => S["The index profile AI memory is stored in and how many memories are read for each question."]);
    }
}
