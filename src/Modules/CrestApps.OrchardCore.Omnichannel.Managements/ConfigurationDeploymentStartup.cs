using CrestApps.OrchardCore.Core.Configuration;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Managements.Deployments;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Managements;

/// <summary>
/// Registers the deployment step that exports Omnichannel configuration.
/// </summary>
[Feature(OmnichannelConstants.Features.Activities)]
[RequireFeatures("OrchardCore.Deployment")]
public sealed class ConfigurationDeploymentStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<OmnichannelConfigurationDeploymentSource, OmnichannelConfigurationDeploymentStep>();
    }
}

/// <summary>
/// Registers the editor for the CRM configuration deployment step. The step itself is headless, so a tenant that runs
/// without an administration surface can still be exported by a script; only the screen that edits the step needs the
/// administration feature.
/// </summary>
[Feature(OmnichannelConstants.Features.Managements)]
[RequireFeatures("OrchardCore.Deployment")]
public sealed class ConfigurationDeploymentAdminStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<DeploymentStep, OmnichannelConfigurationDeploymentStepDisplayDriver>();
    }
}
