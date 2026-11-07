using CrestApps.OrchardCore.Seo.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Deployment;
using OrchardCore.Modules;
using OrchardCore.Settings.Deployment;

namespace CrestApps.OrchardCore.Seo;

/// <summary>
/// Exports the site's structured data settings with a deployment step; Orchard Core's <c>Settings</c> recipe step
/// imports them.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class DeploymentStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteSettingsPropertyDeploymentStep<StructuredDataSettings, DeploymentStartup>(
            S => S["Structured data settings"],
            S => S["The site's Schema.org structured data: whether it is rendered, the publisher, its profiles, and the WebSite and search action entries."]);
    }
}
