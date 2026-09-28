using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Drivers;
using CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Sources;
using CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Steps;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Managements;

/// <summary>
/// Registers the deployment steps that export Omnichannel configuration.
/// </summary>
[Feature(OmnichannelConstants.Features.Activities)]
[RequireFeatures("OrchardCore.Deployment")]
public sealed class ConfigurationDeploymentStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<OmnichannelDispositionDeploymentSource, OmnichannelDispositionDeploymentStep>();
        services.AddDeployment<OmnichannelChannelEndpointDeploymentSource, OmnichannelChannelEndpointDeploymentStep>();
        services.AddDeployment<OmnichannelCampaignGroupDeploymentSource, OmnichannelCampaignGroupDeploymentStep>();
        services.AddDeployment<OmnichannelCampaignDeploymentSource, OmnichannelCampaignDeploymentStep>();
        services.AddDeployment<CadenceDeploymentSource, CadenceDeploymentStep>();
        services.AddDeployment<OmnichannelSubjectActionDeploymentSource, OmnichannelSubjectActionDeploymentStep>();
    }
}

/// <summary>
/// Registers the editors for the Omnichannel configuration deployment steps. The steps themselves are headless, so a
/// tenant that runs without an administration surface can still be exported by a script; only the screens that add the
/// steps to a plan need the administration feature.
/// </summary>
[Feature(OmnichannelConstants.Features.Managements)]
[RequireFeatures("OrchardCore.Deployment")]
public sealed class ConfigurationDeploymentAdminStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<DeploymentStep, OmnichannelDispositionDeploymentStepDisplayDriver>();
        services.AddDisplayDriver<DeploymentStep, OmnichannelChannelEndpointDeploymentStepDisplayDriver>();
        services.AddDisplayDriver<DeploymentStep, OmnichannelCampaignGroupDeploymentStepDisplayDriver>();
        services.AddDisplayDriver<DeploymentStep, OmnichannelCampaignDeploymentStepDisplayDriver>();
        services.AddDisplayDriver<DeploymentStep, CadenceDeploymentStepDisplayDriver>();
        services.AddDisplayDriver<DeploymentStep, OmnichannelSubjectActionDeploymentStepDisplayDriver>();
    }
}
